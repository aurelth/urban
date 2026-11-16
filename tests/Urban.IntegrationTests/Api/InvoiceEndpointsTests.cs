using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using UglyToad.PdfPig;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>O download da fatura em PDF de uma cobrança, de ponta a ponta.</summary>
[Collection(ApiCollection.Name)]
public sealed class InvoiceEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Finance = "/api/finance";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    [Fact]
    public async Task The_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/charges/{Guid.Empty}/invoice", null, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Only_the_manager_gets_in_a_resident_and_the_super_admin_are_refused(UserRole role)
    {
        await MigrateAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: role);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/charges/{Guid.NewGuid()}/invoice", token, null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    [Fact]
    public async Task The_manager_downloads_the_pdf_of_a_charge()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        Guid charge = await FirstChargeAsync(manager, Month(0));

        HttpResponseMessage response = await GetInvoiceAsync(manager, charge);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().MatchRegex(@"^fatura-\d{4}-\d{2}-101\.pdf$");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        using PdfDocument pdf = PdfDocument.Open(bytes);
        string text = string.Join(' ', pdf.GetPages().Select(page => page.Text));
        text.Should().Contain("R$").And.Contain("600,00");
    }

    [Fact]
    public async Task An_unknown_charge_is_404()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await GetInvoiceAsync(manager, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.charge.not_found");
    }

    [Fact]
    public async Task The_charge_of_another_condominium_is_404()
    {
        ManagerSession owner = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, owner, Month(0), "2099-12-10");
        Guid charge = await FirstChargeAsync(owner, Month(0));
        ManagerSession stranger = await RegisterAsync();

        HttpResponseMessage response = await GetInvoiceAsync(stranger, charge);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.charge.not_found");
    }

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private Task<HttpResponseMessage> GetInvoiceAsync(ManagerSession manager, Guid chargeId) =>
        _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/charges/{chargeId}/invoice", manager.AccessToken, null);

    private async Task<Guid> FirstChargeAsync(ManagerSession manager, string month)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/months/{month}/payments", manager.AccessToken, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.GetProperty("items")[0].GetProperty("chargeId").GetGuid();
    }
}
