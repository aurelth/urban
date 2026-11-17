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
/// O envio das faturas por e-mail, de ponta a ponta. O mês fechado tem duas unidades (101 e 102, sem bloco); só a 101 ganha um
/// morador com e-mail. O Hangfire é trocado por um despachante que roda a tarefa na hora, no mesmo caminho que a fila faria
/// (escopo novo, condomínio vindo do id que a fila leva), então o e-mail já está registrado quando o pedido responde.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class InvoiceDeliveryEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Finance = "/api/finance";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string Month(int offset) => ClosedMonthKit.Month(offset);

    private static string MonthRoute(string month) => $"{Finance}/months/{month}/invoice-deliveries";

    private static string ChargeRoute(Guid chargeId) => $"{Finance}/charges/{chargeId}/invoice-deliveries";

    // ---- Acesso ----

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task The_month_route_requires_a_token(string method)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), MonthRoute("2026-10"), null, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_charge_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, ChargeRoute(Guid.Empty), null, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", UserRole.Resident)]
    [InlineData("POST", UserRole.Resident)]
    [InlineData("GET", UserRole.SuperAdmin)]
    [InlineData("POST", UserRole.SuperAdmin)]
    public async Task Only_the_manager_gets_in_a_resident_and_the_super_admin_are_refused(string method, UserRole role)
    {
        await MigrateAsync();
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: role);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage month = await _client.SendJsonAsync(new HttpMethod(method), MonthRoute("2026-10"), token, null);
        HttpResponseMessage charge = await _client.SendJsonAsync(HttpMethod.Post, ChargeRoute(Guid.NewGuid()), token, null);

        month.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await month.CodeAsync()).Should().Be("identity.user.not_manager");
        charge.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Pedidos inválidos ----

    [Fact]
    public async Task A_month_that_is_not_closed_is_a_conflict_for_both_the_list_and_the_send()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage list = await GetAsync(manager, MonthRoute(Month(0)));
        HttpResponseMessage send = await PostAsync(manager, MonthRoute(Month(0)));

        list.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await list.CodeAsync()).Should().Be("closing.not_closed");
        send.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("outubro")]
    [InlineData("2026-13")]
    public async Task A_month_that_is_not_a_month_is_a_bad_request(string month)
    {
        ManagerSession manager = await RegisterAsync();

        (await GetAsync(manager, MonthRoute(month))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PostAsync(manager, MonthRoute(month))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=201")]
    public async Task A_page_out_of_range_is_a_bad_request(string query)
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");

        HttpResponseMessage response = await GetAsync(manager, $"{MonthRoute(Month(0))}?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("finance.invoice_delivery.page_invalid");
    }

    // ---- O envio do mês ----

    [Fact]
    public async Task Before_anything_is_sent_each_unit_is_not_sent_or_has_no_recipient()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        await CreateResidentAsync(manager, "101", $"morador-{Guid.NewGuid():N}@exemplo.com");

        HttpResponseMessage response = await GetAsync(manager, MonthRoute(Month(0)));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("month").GetString().Should().Be(Month(0));
        body.GetProperty("total").GetInt32().Should().Be(2);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(50);
        JsonElement counts = body.GetProperty("counts");
        counts.GetProperty("notSent").GetInt32().Should().Be(1);
        counts.GetProperty("noRecipient").GetInt32().Should().Be(1);
        counts.GetProperty("sent").GetInt32().Should().Be(0);
        JsonElement[] items = [.. body.GetProperty("items").EnumerateArray()];
        items.Select(item => item.GetProperty("number").GetString()).Should().Equal("101", "102");
        items[0].GetProperty("state").GetString().Should().Be("notSent");
        items[0].GetProperty("responsibleName").GetString().Should().Be("Maria Souza");
        items[1].GetProperty("state").GetString().Should().Be("noRecipient");
        items[1].GetProperty("responsibleName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Sending_the_month_mails_the_pdf_to_the_responsible_and_leaves_out_the_unit_without_an_email()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, "101", address);

        HttpResponseMessage response = await PostAsync(manager, MonthRoute(Month(0)));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("queued").GetInt32().Should().Be(1);
        summary.GetProperty("withoutRecipient").GetInt32().Should().Be(1);
        summary.GetProperty("alreadySent").GetInt32().Should().Be(0);
        summary.GetProperty("alreadyQueued").GetInt32().Should().Be(0);

        EmailMessage message = fixture.Factory.Emails.SentTo(address).Should().ContainSingle().Subject;
        message.Subject.Should().StartWith("Fatura de ");
        message.TextBody.Should().Contain("Olá, Maria Souza.").And.Contain("R$ 600,00").And.Contain("Vencimento: 10/12/2099");
        EmailAttachment attachment = message.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().MatchRegex(@"^fatura-\d{4}-\d{2}-101\.pdf$");
        Encoding.ASCII.GetString(attachment.Content, 0, 5).Should().Be("%PDF-");

        JsonElement[] items = [.. (await ReadListAsync(manager, Month(0))).GetProperty("items").EnumerateArray()];
        items[0].GetProperty("state").GetString().Should().Be("sent");
        items[0].GetProperty("sentAt").ValueKind.Should().Be(JsonValueKind.String);
        items[1].GetProperty("state").GetString().Should().Be("noRecipient");
    }

    [Fact]
    public async Task Sending_the_month_again_does_not_mail_the_same_invoice_twice()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, "101", address);
        (await PostAsync(manager, MonthRoute(Month(0)))).StatusCode.Should().Be(HttpStatusCode.Accepted);

        HttpResponseMessage second = await PostAsync(manager, MonthRoute(Month(0)));

        second.StatusCode.Should().Be(HttpStatusCode.Accepted);
        JsonElement summary = await second.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("queued").GetInt32().Should().Be(0);
        summary.GetProperty("alreadySent").GetInt32().Should().Be(1);
        fixture.Factory.Emails.SentTo(address).Should().HaveCount(1);
    }

    [Fact]
    public async Task When_the_provider_refuses_the_unit_is_marked_failed_and_the_next_send_tries_again()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, "101", address);

        fixture.Factory.Emails.RefuseDeliveries = true;

        try
        {
            (await PostAsync(manager, MonthRoute(Month(0)))).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
        finally
        {
            fixture.Factory.Emails.RefuseDeliveries = false;
        }

        JsonElement failed = (await ReadListAsync(manager, Month(0))).GetProperty("items")[0];
        failed.GetProperty("state").GetString().Should().Be("failed");
        failed.GetProperty("failureReason").GetString().Should().Be("not_delivered");
        fixture.Factory.Emails.SentTo(address).Should().BeEmpty();

        HttpResponseMessage again = await PostAsync(manager, MonthRoute(Month(0)));

        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("queued").GetInt32().Should().Be(1);
        (await ReadListAsync(manager, Month(0))).GetProperty("items")[0].GetProperty("state").GetString().Should().Be("sent");
        fixture.Factory.Emails.SentTo(address).Should().HaveCount(1);
    }

    [Fact]
    public async Task The_list_pages_the_units()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");

        JsonElement second = await ReadListAsync(manager, Month(0), "?page=2&pageSize=1");

        second.GetProperty("total").GetInt32().Should().Be(2);
        second.GetProperty("items").GetArrayLength().Should().Be(1);
        second.GetProperty("items")[0].GetProperty("number").GetString().Should().Be("102");
    }

    // ---- O reenvio de uma unidade ----

    [Fact]
    public async Task A_unit_that_already_received_can_be_resent_on_request()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(manager, "101", address);
        await PostAsync(manager, MonthRoute(Month(0)));
        Guid charge = await ChargeOfAsync(manager, Month(0), "101");

        HttpResponseMessage response = await PostAsync(manager, ChargeRoute(charge));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        fixture.Factory.Emails.SentTo(address).Should().HaveCount(2);
        (await ReadListAsync(manager, Month(0))).GetProperty("items")[0].GetProperty("state").GetString().Should().Be("sent");
    }

    [Fact]
    public async Task Resending_to_a_unit_without_a_responsible_with_an_email_is_refused()
    {
        ManagerSession manager = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, manager, Month(0), "2099-12-10");
        Guid charge = await ChargeOfAsync(manager, Month(0), "102");

        HttpResponseMessage response = await PostAsync(manager, ChargeRoute(charge));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.CodeAsync()).Should().Be("finance.invoice_delivery.no_recipient");
    }

    [Fact]
    public async Task An_unknown_charge_is_404()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await PostAsync(manager, ChargeRoute(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("finance.charge.not_found");
    }

    // ---- Isolamento entre condomínios ----

    [Fact]
    public async Task The_charge_and_the_deliveries_of_another_condominium_do_not_exist_for_a_stranger()
    {
        ManagerSession owner = await RegisterAsync();
        await ClosedMonthKit.CloseAsync(_client, owner, Month(0), "2099-12-10");
        string address = $"morador-{Guid.NewGuid():N}@exemplo.com";
        await CreateResidentAsync(owner, "101", address);
        await PostAsync(owner, MonthRoute(Month(0)));
        Guid charge = await ChargeOfAsync(owner, Month(0), "101");
        ManagerSession stranger = await RegisterAsync();

        HttpResponseMessage resend = await PostAsync(stranger, ChargeRoute(charge));
        HttpResponseMessage list = await GetAsync(stranger, MonthRoute(Month(0)));
        HttpResponseMessage send = await PostAsync(stranger, MonthRoute(Month(0)));

        resend.StatusCode.Should().Be(HttpStatusCode.NotFound);
        list.StatusCode.Should().Be(HttpStatusCode.Conflict, "o mês do outro condomínio não existe para quem pergunta");
        send.StatusCode.Should().Be(HttpStatusCode.Conflict);
        fixture.Factory.Emails.SentTo(address).Should().HaveCount(1);
    }

    // ---- Apoio ----

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }

    private Task<HttpResponseMessage> GetAsync(ManagerSession manager, string route) => _client.SendJsonAsync(HttpMethod.Get, route, manager.AccessToken, null);

    private Task<HttpResponseMessage> PostAsync(ManagerSession manager, string route) => _client.SendJsonAsync(HttpMethod.Post, route, manager.AccessToken, null);

    private async Task<JsonElement> ReadListAsync(ManagerSession manager, string month, string query = "")
    {
        HttpResponseMessage response = await GetAsync(manager, MonthRoute(month) + query);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<Guid> ChargeOfAsync(ManagerSession manager, string month, string number)
    {
        JsonElement list = await ReadListAsync(manager, month);

        return list.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("number").GetString() == number).GetProperty("chargeId").GetGuid();
    }

    private async Task CreateResidentAsync(ManagerSession manager, string unitNumber, string email)
    {
        HttpResponseMessage units = await GetAsync(manager, "/api/units");
        Guid unitId = (await units.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray()
            .Single(unit => unit.GetProperty("number").GetString() == unitNumber).GetProperty("id").GetGuid();
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
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
    }
}
