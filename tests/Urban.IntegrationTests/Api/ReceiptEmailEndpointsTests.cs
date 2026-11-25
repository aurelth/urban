using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Finance.Infrastructure;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>
/// O envio do recibo por e-mail, de ponta a ponta. O mês fechado tem as unidades 101 (cobrança de 600) e 102 (400), sem bloco; só a 101
/// ganha uma moradora com e-mail. O Hangfire é trocado por um despachante que roda a tarefa na hora, então o e-mail já está
/// registrado quando o pedido responde.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReceiptEmailEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Finance = "/api/finance";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    private static string Route(Guid paymentId) => $"{Finance}/payments/{paymentId}/receipt-deliveries";

    // ---- Acesso ----

    [Fact]
    public async Task The_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route(Guid.Empty), null, null);

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

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route(Guid.NewGuid()), token, null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    // ---- O envio ----

    [Fact]
    public async Task The_manager_sends_the_receipt_and_the_responsible_of_the_unit_gets_it_with_the_pdf_attached()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, units["101"], address);
        Guid payment = await PaymentOfAsync(manager, units["101"], 250m);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route(payment), manager.AccessToken, null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("recipientName").GetString().Should().Be("Maria Souza");
        EmailMessage message = fixture.Factory.Emails.SentTo(address).Should().ContainSingle().Subject;
        message.Subject.Should().StartWith("Recibo REC-000001 - ");
        message.TextBody.Should().Contain("Olá, Maria Souza.").And.Contain("Valor recebido: R$ 250,00").And.NotContain("Anotação interna");
        EmailAttachment attachment = message.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("recibo-000001.pdf");
        Encoding.ASCII.GetString(attachment.Content.ToArray(), 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task The_manager_may_send_it_again_and_the_responsible_gets_a_second_email()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, units["101"], address);
        Guid payment = await PaymentOfAsync(manager, units["101"], 100m);

        HttpResponseMessage first = await _client.SendJsonAsync(HttpMethod.Post, Route(payment), manager.AccessToken, null);
        HttpResponseMessage second = await _client.SendJsonAsync(HttpMethod.Post, Route(payment), manager.AccessToken, null);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        fixture.Factory.Emails.SentTo(address).Should().HaveCount(2);
    }

    [Fact]
    public async Task A_unit_without_a_responsible_with_email_is_a_422()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        Guid payment = await PaymentOfAsync(manager, units["102"], 100m);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route(payment), manager.AccessToken, null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.CodeAsync()).Should().Be("finance.receipt_delivery.no_recipient");
    }

    [Fact]
    public async Task A_reversed_payment_is_a_409_and_nothing_is_sent()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, units["101"], address);
        Guid payment = await PaymentOfAsync(manager, units["101"], 100m);
        HttpResponseMessage reversed = await _client.SendJsonAsync(
            HttpMethod.Post, $"{Finance}/payments/{payment}/reverse", manager.AccessToken, new JsonObject { ["reason"] = "Unidade errada" });
        reversed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route(payment), manager.AccessToken, null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("finance.receipt_delivery.payment_reversed");
        fixture.Factory.Emails.SentTo(address).Should().BeEmpty();
    }

    [Fact]
    public async Task A_payment_that_does_not_exist_is_a_404()
    {
        (ManagerSession manager, _) = await ClosedMonthAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route(Guid.NewGuid()), manager.AccessToken, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.payment.not_found");
    }

    [Fact]
    public async Task A_payment_of_another_condominium_is_a_404_and_nothing_is_sent()
    {
        (ManagerSession manager, Dictionary<string, Guid> units) = await ClosedMonthAsync();
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, units["101"], address);
        Guid payment = await PaymentOfAsync(manager, units["101"], 100m);
        ManagerSession stranger = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, Route(payment), stranger.AccessToken, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        fixture.Factory.Emails.SentTo(address).Should().BeEmpty();
    }

    // ---- Apoio ----

    private async Task<(ManagerSession Manager, Dictionary<string, Guid> Units)> ClosedMonthAsync()
    {
        await fixture.Factory.Services.MigrateFinanceAsync();
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        JsonElement body = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, "/api/units", manager.AccessToken));

        return (manager, body.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("number").GetString()!, item => item.GetProperty("id").GetGuid()));
    }

    private async Task<Guid> PaymentOfAsync(ManagerSession manager, Guid unitId, decimal amount)
    {
        JsonElement list = await ReadAsync(await _client.SendJsonAsync(HttpMethod.Get, $"{Finance}/months/{Month(0)}/payments", manager.AccessToken));
        Guid chargeId = list.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("unitId").GetGuid() == unitId).GetProperty("chargeId").GetGuid();
        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Post,
            $"{Finance}/charges/{chargeId}/payments",
            manager.AccessToken,
            new JsonObject { ["amount"] = amount, ["receivedOn"] = ClosedMonthKit.Today(), ["method"] = "pix", ["note"] = "Anotação interna" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));

        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private async Task CreateResidentAsync(ManagerSession manager, Guid unitId, string email)
    {
        JsonObject body = new()
        {
            ["fullName"] = "Maria Souza",
            ["cpf"] = CondominiumBuilders.UniqueCpf().Value,
            ["birthDate"] = "1990-05-20",
            ["phone"] = "(47) 99876-5432",
            ["email"] = email,
            ["profession"] = "Engenheira",
            ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
            ["unitId"] = unitId,
            ["kind"] = "owner",
            ["startDate"] = "2024-03-01",
        };

        HttpResponseMessage created = await _client.SendJsonAsync(HttpMethod.Post, "/api/residents", manager.AccessToken, body);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));

        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
