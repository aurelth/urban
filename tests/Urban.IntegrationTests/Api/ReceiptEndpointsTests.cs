using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using UglyToad.PdfPig;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// O recibo de um pagamento, de ponta a ponta: o número que o pagamento ganha ao ser registrado e o PDF, que o síndico baixa de
/// qualquer pagamento do condomínio e o morador, só dos da unidade em que mora. O mês fechado tem as unidades 101 (cobrança de 600) e 102 (400).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReceiptEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Finance = "/api/finance";
    private const string MeFinance = "/api/finance/me";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    private static string ManagerRoute(Guid paymentId) => $"{Finance}/payments/{paymentId}/receipt";

    private static string ResidentRoute(Guid paymentId) => $"{MeFinance}/payments/{paymentId}/receipt";

    // ---- O número no pagamento ----

    [Fact]
    public async Task A_registered_payment_comes_back_with_its_receipt_number_and_code()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        Guid charge = await ChargeOfAsync(manager, units["101"]);

        JsonElement first = await RegisterAsync(manager, charge, 100m);
        JsonElement second = await RegisterAsync(manager, charge, 50m);

        first.GetProperty("receiptNumber").GetInt32().Should().Be(1);
        first.GetProperty("receiptCode").GetString().Should().Be("REC-000001");
        second.GetProperty("receiptNumber").GetInt32().Should().Be(2);
        second.GetProperty("receiptCode").GetString().Should().Be("REC-000002");
    }

    [Fact]
    public async Task The_list_of_payments_of_a_charge_shows_the_receipt_of_each_one()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        Guid charge = await ChargeOfAsync(manager, units["101"]);
        await RegisterAsync(manager, charge, 100m);

        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/charges/{charge}/payments", manager.AccessToken));

        JsonElement payment = body.GetProperty("payments").EnumerateArray().Single();
        payment.GetProperty("receiptNumber").GetInt32().Should().Be(1);
        payment.GetProperty("receiptCode").GetString().Should().Be("REC-000001");
    }

    // ---- O PDF do síndico ----

    [Fact]
    public async Task The_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerRoute(Guid.Empty), null, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Only_the_manager_gets_in_a_resident_and_the_super_admin_are_refused(UserRole role)
    {
        await fixture.Factory.Services.MigrateFinanceAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: role);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerRoute(Guid.NewGuid()), token, null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    [Fact]
    public async Task The_manager_downloads_the_pdf_of_a_payment()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        Guid charge = await ChargeOfAsync(manager, units["101"]);
        Guid payment = (await RegisterAsync(manager, charge, 250m)).GetProperty("id").GetGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerRoute(payment), manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("recibo-000001.pdf");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        string text = TextOf(bytes);
        text.Should().Contain("REC-000001").And.Contain("250,00").And.Contain("350,00");
        text.Should().NotContain("CANCELADO");
    }

    [Fact]
    public async Task A_payment_that_does_not_exist_is_a_404()
    {
        (ManagerSession manager, _) = await ClosedMonthAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerRoute(Guid.NewGuid()), manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.payment.not_found");
    }

    [Fact]
    public async Task A_payment_of_another_condominium_is_a_404()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        Guid payment = (await RegisterAsync(manager, await ChargeOfAsync(manager, units["101"]), 100m)).GetProperty("id").GetGuid();
        ManagerSession stranger = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerRoute(payment), stranger.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_receipt_of_a_reversed_payment_is_still_there_stamped_as_canceled()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        Guid payment = (await RegisterAsync(manager, await ChargeOfAsync(manager, units["101"]), 100m)).GetProperty("id").GetGuid();
        HttpResponseMessage reversed = await _client.SendJsonAsync(
            HttpMethod.Post, $"{Finance}/payments/{payment}/reverse", manager.AccessToken, new JsonObject { ["reason"] = "Unidade errada" });
        reversed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ManagerRoute(payment), manager.AccessToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string text = TextOf(await response.Content.ReadAsByteArrayAsync(Ct));
        text.Should().Contain("CANCELADO").And.Contain("REC-000001");
        text.Should().NotContain("Unidade errada");
    }

    // ---- O PDF do morador ----

    [Fact]
    public async Task The_resident_downloads_the_receipt_of_a_payment_of_his_own_unit()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid payment = (await RegisterAsync(manager, await ChargeOfAsync(manager, units["101"]), 100m)).GetProperty("id").GetGuid();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ResidentRoute(payment), token);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("recibo-000001.pdf");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync(Ct), 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task The_resident_cannot_download_the_receipt_of_another_unit_and_it_looks_like_the_payment_does_not_exist()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        string token = await ResidentTokenAsync(manager, units["101"]);
        Guid neighbour = (await RegisterAsync(manager, await ChargeOfAsync(manager, units["102"]), 100m)).GetProperty("id").GetGuid();

        HttpResponseMessage other = await _client.SendJsonAsync(HttpMethod.Get, ResidentRoute(neighbour), token);
        HttpResponseMessage missing = await _client.SendJsonAsync(HttpMethod.Get, ResidentRoute(Guid.NewGuid()), token);

        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.CodeAsync()).Should().Be("finance.payment.not_found");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await missing.CodeAsync()).Should().Be("finance.payment.not_found");
    }

    [Fact]
    public async Task A_dependent_and_a_manager_without_a_unit_cannot_download_any_receipt_here()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        string dependent = await ResidentTokenAsync(manager, units["101"], "dependent");
        Guid payment = (await RegisterAsync(manager, await ChargeOfAsync(manager, units["101"]), 100m)).GetProperty("id").GetGuid();

        HttpResponseMessage forTheDependent = await _client.SendJsonAsync(HttpMethod.Get, ResidentRoute(payment), dependent);
        HttpResponseMessage forTheManager = await _client.SendJsonAsync(HttpMethod.Get, ResidentRoute(payment), manager.AccessToken);

        forTheDependent.StatusCode.Should().Be(HttpStatusCode.NotFound);
        forTheManager.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_resident_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, ResidentRoute(Guid.Empty), null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- Apoio ----

    private static string TextOf(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);

        return string.Join(' ', document.GetPages().Select(page => page.Text));
    }

    private async Task<(ManagerSession Manager, Dictionary<string, Guid> Units)> ClosedMonthAsync()
    {
        await fixture.Factory.Services.MigrateFinanceAsync();
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, "/api/units", manager.AccessToken));

        return (manager, body.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("number").GetString()!, item => item.GetProperty("id").GetGuid()));
    }

    private Task<string> ResidentTokenAsync(ManagerSession manager, Guid unitId, string kind = "owner") =>
        ResidentSetup.TokenAsync(fixture, _client, manager, unitId, kind);

    private async Task<Guid> ChargeOfAsync(ManagerSession manager, Guid unitId)
    {
        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/months/{Month(0)}/payments", manager.AccessToken));

        return body.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("unitId").GetGuid() == unitId).GetProperty("chargeId").GetGuid();
    }

    private async Task<JsonElement> RegisterAsync(ManagerSession manager, Guid chargeId, decimal amount)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post,
            $"{Finance}/charges/{chargeId}/payments",
            manager.AccessToken,
            new JsonObject { ["amount"] = amount, ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "pix", ["note"] = "Anotação interna" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
