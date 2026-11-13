using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class AdministrationEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string SummaryRoute = "/api/admin/summary";
    private const string ManagersRoute = "/api/admin/managers";
    private const string Password = "Senha-segura-123";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    [Theory]
    [InlineData("/api/admin/summary", "get")]
    [InlineData("/api/admin/managers", "get")]
    public async Task Reading_without_a_session_returns_401(string route, string method)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), route, accessToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/admin/summary")]
    [InlineData("/api/admin/managers")]
    public async Task Reading_as_a_manager_returns_403_with_a_stable_code(string route)
    {
        string token = await ManagerTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, route, token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.administration.forbidden");
    }

    [Fact]
    public async Task The_summary_shows_the_numbers_of_the_platform_and_is_never_cached()
    {
        string token = await SuperAdminTokenAsync();
        JsonElement before = await ReadSummaryAsync(token);
        User suspended = await SeedManagerAsync(AccountStatus.Suspended);
        await SeedManagerAsync(AccountStatus.Active);
        await RequestAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, SummaryRoute, token);
        JsonElement after = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (after.GetProperty("activeManagers").GetInt32() - before.GetProperty("activeManagers").GetInt32()).Should().Be(1);
        (after.GetProperty("suspendedManagers").GetInt32() - before.GetProperty("suspendedManagers").GetInt32()).Should().Be(1);
        (after.GetProperty("pendingRequests").GetInt32() - before.GetProperty("pendingRequests").GetInt32()).Should().Be(1);
        after.TryGetProperty("awaitingConfirmationManagers", out _).Should().BeTrue();
        after.TryGetProperty("condominiums", out _).Should().BeTrue();
        suspended.Status.Should().Be(AccountStatus.Suspended);
    }

    [Fact]
    public async Task The_list_shows_each_manager_with_the_real_name_of_the_condominium()
    {
        await fixture.Factory.Services.MigrateCondominiumAsync();
        ManagerSession session = await ManagerSetup.RegisterAsync(fixture, _client);
        string token = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{ManagersRoute}?search={Uri.EscapeDataString(session.User.Email.Value)}", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().Be(1);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        JsonElement item = body.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().Should().Be(session.User.Id);
        item.GetProperty("email").GetString().Should().Be(session.User.Email.Value);
        item.GetProperty("name").GetString().Should().Be("Ana");
        item.GetProperty("condominiumId").GetGuid().Should().Be(session.CondominiumId);
        item.GetProperty("condominiumName").GetString().Should().Be("Residencial Vila Verde");
        item.GetProperty("status").GetString().Should().Be("Active");
        item.GetProperty("statusReason").ValueKind.Should().Be(JsonValueKind.Null);
        User stored = await ReadAsync(session.User.Id);
        item.GetProperty("createdAt").GetDateTimeOffset().Should().Be(stored.CreatedAt);
        item.GetProperty("statusChangedAt").GetDateTimeOffset().Should().Be(stored.StatusChangedAt);
    }

    [Fact]
    public async Task The_list_filters_by_status_and_shows_the_reason_of_a_suspension()
    {
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User suspended = await SeedManagerAsync(AccountStatus.Suspended);
        string token = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{ManagersRoute}?status=suspended&search={Uri.EscapeDataString(suspended.Email.Value)}", token);

        JsonElement item = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("status").GetString().Should().Be("Suspended");
        item.GetProperty("statusReason").GetString().Should().Be("Motivo de teste");
    }

    [Fact]
    public async Task The_list_does_not_show_requests_waiting_for_a_decision()
    {
        await fixture.Factory.Services.MigrateCondominiumAsync();
        string address = NewAddress();
        await RequestAsync(address);
        string token = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{ManagersRoute}?search={Uri.EscapeDataString(address)}", token);

        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32().Should().Be(0);
    }

    [Theory]
    [InlineData("status=bogus", "request.invalid")]
    [InlineData("status=Deleted", "identity.manager.status_filter_invalid")]
    [InlineData("status=PendingApproval", "identity.manager.status_filter_invalid")]
    [InlineData("page=0", "identity.manager.page_invalid")]
    [InlineData("pageSize=0", "identity.manager.page_invalid")]
    [InlineData("pageSize=101", "identity.manager.page_invalid")]
    public async Task The_list_rejects_filters_and_pages_it_cannot_serve_with_400(string query, string code)
    {
        string token = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{ManagersRoute}?{query}", token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task Suspending_records_the_reason_and_ends_the_sessions_of_the_manager()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        HttpResponseMessage login = await _client.LoginAsync(manager.Email.Value, Password);
        string accessToken = await login.AccessTokenAsync();
        string? cookie = login.RefreshCookieValue();
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/suspend", adminToken, new { reason = "  Uso indevido da plataforma  " });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        User stored = await ReadAsync(manager.Id);
        stored.Status.Should().Be(AccountStatus.Suspended);
        stored.StatusReason.Should().Be("Uso indevido da plataforma");
        (await _client.MeAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.RefreshAsync(cookie)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        HttpResponseMessage again = await _client.LoginAsync(manager.Email.Value, Password);
        again.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await again.CodeAsync()).Should().Be("identity.user.account_suspended");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Suspending_without_a_reason_returns_400_and_changes_nothing(string? reason)
    {
        User manager = await SeedManagerAsync(AccountStatus.Active);
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/suspend", adminToken, new { reason });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.user.reason_required");
        (await ReadAsync(manager.Id)).Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public async Task Suspending_without_a_body_returns_400_with_the_same_code()
    {
        User manager = await SeedManagerAsync(AccountStatus.Active);
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/suspend", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.user.reason_required");
    }

    [Fact]
    public async Task Suspending_with_a_reason_that_is_too_long_returns_400()
    {
        User manager = await SeedManagerAsync(AccountStatus.Active);
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/suspend", adminToken, new { reason = new string('a', 501) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.user.reason_too_long");
    }

    [Fact]
    public async Task Suspending_twice_returns_409()
    {
        User manager = await SeedManagerAsync(AccountStatus.Active);
        string adminToken = await SuperAdminTokenAsync();
        await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/suspend", adminToken, new { reason = "Motivo" });

        HttpResponseMessage second = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/suspend", adminToken, new { reason = "Motivo" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.CodeAsync()).Should().Be(UserErrors.InvalidTransition.Code);
    }

    [Fact]
    public async Task Reactivating_brings_the_manager_back_and_clears_the_reason()
    {
        User manager = await fixture.Postgres.SeedUserWithPasswordAsync(Password, AccountStatus.Suspended, UserRole.Manager);
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/reactivate", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        User stored = await ReadAsync(manager.Id);
        stored.Status.Should().Be(AccountStatus.Active);
        stored.StatusReason.Should().BeNull();
        (await _client.LoginAsync(manager.Email.Value, Password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reactivating_a_manager_who_is_not_suspended_returns_409()
    {
        User manager = await SeedManagerAsync(AccountStatus.Active);
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{manager.Id}/reactivate", adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be(UserErrors.InvalidTransition.Code);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task Changing_an_unknown_account_returns_404(string action)
    {
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{Guid.NewGuid()}/{action}", adminToken, new { reason = "Motivo" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("identity.manager.not_found");
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task Changing_an_account_that_is_not_a_manager_returns_404_and_changes_nothing(string action)
    {
        User resident = await fixture.Postgres.SeedUserAsync(AccountStatus.Active, UserRole.Resident);
        string adminToken = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{resident.Id}/{action}", adminToken, new { reason = "Motivo" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadAsync(resident.Id)).Status.Should().Be(AccountStatus.Active);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task Changing_as_a_manager_returns_403_and_changes_nothing(string action)
    {
        User target = await SeedManagerAsync(AccountStatus.Active);
        string token = await ManagerTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{target.Id}/{action}", token, new { reason = "Motivo" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadAsync(target.Id)).Status.Should().Be(AccountStatus.Active);
    }

    [Theory]
    [InlineData("suspend")]
    [InlineData("reactivate")]
    public async Task Changing_without_a_session_returns_401(string action)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{ManagersRoute}/{Guid.NewGuid()}/{action}", accessToken: null, new { reason = "Motivo" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string NewAddress() => $"{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> RequestAsync(string? address = null) =>
        _client.PostAsJsonAsync("/api/auth/register", new { email = address ?? NewAddress(), name = "Ana Paula", condominiumName = "Residencial Vila Verde" });

    private Task<User> SeedManagerAsync(AccountStatus status) => fixture.Postgres.SeedUserAsync(status, UserRole.Manager);

    private async Task<JsonElement> ReadSummaryAsync(string token)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, SummaryRoute, token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<User> ReadAsync(Guid id)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    private async Task<string> SuperAdminTokenAsync()
    {
        User admin = await fixture.Postgres.SeedUserWithPasswordAsync(Password, AccountStatus.Active, UserRole.SuperAdmin);
        return await _client.SignInAsync(admin);
    }

    private async Task<string> ManagerTokenAsync()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        return await _client.SignInAsync(manager);
    }
}
