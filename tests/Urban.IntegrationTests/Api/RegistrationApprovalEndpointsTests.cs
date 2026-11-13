using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.Identity.Infrastructure.Tokens;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class RegistrationApprovalEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Route = "/api/admin/registration-requests";
    private const string Password = "Senha-segura-123";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });
    private readonly ISecretGenerator _secrets = new SecretGenerator();

    private RecordingEmailSender Emails => fixture.Factory.Emails;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Listing_without_a_session_returns_401()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route, accessToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Listing_as_a_manager_returns_403_with_a_stable_code()
    {
        string token = await ManagerTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route, token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.registration_request.forbidden");
    }

    [Fact]
    public async Task Listing_as_the_super_admin_shows_the_pending_requests_with_the_data_the_person_typed()
    {
        string address = NewAddress();
        await RequestAsync(address, "Ana Paula", "Residencial Vila Verde");
        string token = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route, token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement item = Find(await response.Content.ReadFromJsonAsync<JsonElement>(), address);
        item.GetProperty("name").GetString().Should().Be("Ana Paula");
        item.GetProperty("condominiumName").GetString().Should().Be("Residencial Vila Verde");
        item.GetProperty("id").GetGuid().Should().NotBeEmpty();
        item.GetProperty("requestedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Approving_moves_the_request_forward_and_sends_the_confirmation_link_to_the_person()
    {
        string address = NewAddress();
        await RequestAsync(address);
        Emails.SentTo(address).Should().BeEmpty();

        HttpResponseMessage response = await DecideAsync("approve", address);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadUserAsync(address)).Status.Should().Be(AccountStatus.PendingVerification);
        EmailMessage message = Emails.SentTo(address).Should().ContainSingle().Subject;
        message.TextBody.Should().Contain("https://app.test/confirmar-email?token=");
    }

    [Fact]
    public async Task Approving_stores_only_the_hash_of_the_link_secret()
    {
        string address = NewAddress();
        await RequestAsync(address);
        await DecideAsync("approve", address);
        string secret = RecordingEmailSender.TokenIn(Emails.SentTo(address).Single());

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        string[] stored = await db.EmailVerificationTokens.AsNoTracking()
            .Where(t => db.Users.Any(u => u.Id == t.UserId && u.Email == Email.Create(address).Value))
            .Select(t => t.TokenHash)
            .ToArrayAsync();

        stored.Should().ContainSingle().Which.Should().Be(_secrets.Hash(secret)).And.NotBe(secret);
    }

    [Fact]
    public async Task An_approved_person_confirms_the_email_chooses_a_password_and_logs_in()
    {
        string address = NewAddress();
        await RequestAsync(address);
        await DecideAsync("approve", address);
        string link = RecordingEmailSender.TokenIn(Emails.SentTo(address).Single());

        HttpResponseMessage confirmed = await _client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new { token = link, password = Password, termsVersion = fixture.Factory.TermsVersion });
        HttpResponseMessage login = await _client.LoginAsync(address, Password);

        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Approving_twice_returns_409_and_sends_only_one_link()
    {
        string address = NewAddress();
        await RequestAsync(address);
        await DecideAsync("approve", address);

        HttpResponseMessage second = await DecideAsync("approve", address);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.CodeAsync()).Should().Be(UserErrors.InvalidTransition.Code);
        Emails.SentTo(address).Should().ContainSingle();
    }

    [Fact]
    public async Task Rejecting_marks_the_request_as_rejected_and_sends_nothing_to_the_person()
    {
        string address = NewAddress();
        await RequestAsync(address);

        HttpResponseMessage response = await DecideAsync("reject", address);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ReadUserAsync(address)).Status.Should().Be(AccountStatus.Rejected);
        Emails.SentTo(address).Should().BeEmpty();
    }

    [Fact]
    public async Task A_rejected_request_leaves_the_pending_list()
    {
        string address = NewAddress();
        await RequestAsync(address);
        await DecideAsync("reject", address);
        string token = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route, token);

        Find(await response.Content.ReadFromJsonAsync<JsonElement>(), address).ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public async Task Asking_again_after_a_rejection_still_answers_202_and_changes_nothing()
    {
        string address = NewAddress();
        await RequestAsync(address);
        await DecideAsync("reject", address);

        HttpResponseMessage again = await RequestAsync(address, "Ana Paula", "Outro Condomínio");

        again.StatusCode.Should().Be(HttpStatusCode.Accepted);
        User user = await ReadUserAsync(address);
        user.Status.Should().Be(AccountStatus.Rejected);
        user.RequestedCondominiumName!.Value.Should().Be("Residencial Vila Verde");
        Emails.SentTo(address).Should().BeEmpty();
    }

    [Fact]
    public async Task Rejecting_after_approving_returns_409()
    {
        string address = NewAddress();
        await RequestAsync(address);
        await DecideAsync("approve", address);

        HttpResponseMessage response = await DecideAsync("reject", address);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadUserAsync(address)).Status.Should().Be(AccountStatus.PendingVerification);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Deciding_an_unknown_request_returns_404(string action)
    {
        string token = await SuperAdminTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Route}/{Guid.NewGuid()}/{action}", token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("identity.registration_request.not_found");
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Deciding_as_a_manager_returns_403_and_changes_nothing(string action)
    {
        string address = NewAddress();
        await RequestAsync(address);
        Guid id = (await ReadUserAsync(address)).Id;
        string token = await ManagerTokenAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Route}/{id}/{action}", token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadUserAsync(address)).Status.Should().Be(AccountStatus.PendingApproval);
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Deciding_without_a_session_returns_401(string action)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, $"{Route}/{Guid.NewGuid()}/{action}", accessToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_new_request_notifies_the_owner_by_email_with_the_data_and_the_review_link()
    {
        string owner = NewAddress();
        await using ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = ApiFactory.UnusedRedis,
            SuperAdminEmail = owner,
        };
        using HttpClient client = factory.CreateClient();
        string address = NewAddress();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = address, name = "Ana Paula", condominiumName = "Residencial Vila Verde" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        EmailMessage notice = factory.Emails.SentTo(owner).Single(m => m.Subject == "Novo pedido de cadastro no Urban");
        notice.TextBody.Should().Contain("Ana Paula").And.Contain(address).And.Contain("Residencial Vila Verde");
        notice.TextBody.Should().Contain("https://app.test/admin/pedidos-de-cadastro");
        factory.Emails.SentTo(address).Should().BeEmpty();
    }

    private static string NewAddress() => $"{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> RequestAsync(string address, string name = "Ana Paula", string condominiumName = "Residencial Vila Verde") =>
        _client.PostAsJsonAsync("/api/auth/register", new { email = address, name, condominiumName });

    private async Task<HttpResponseMessage> DecideAsync(string action, string address)
    {
        Guid id = (await ReadUserAsync(address)).Id;
        string token = await SuperAdminTokenAsync();
        return await _client.SendJsonAsync(HttpMethod.Post, $"{Route}/{id}/{action}", token);
    }

    private async Task<User> ReadUserAsync(string address)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email.Create(address).Value);
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

    private static JsonElement Find(JsonElement list, string address)
    {
        foreach (JsonElement item in list.EnumerateArray())
        {
            if (item.GetProperty("email").GetString() == address)
            {
                return item;
            }
        }

        return default;
    }
}
