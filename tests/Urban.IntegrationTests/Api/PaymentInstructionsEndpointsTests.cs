using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Finance.Infrastructure;

namespace Urban.IntegrationTests.Api;

/// <summary>Os dados para pagamento que o síndico cadastra e a fatura mostra: chave Pix e conta bancária do condomínio.</summary>
[Collection(ApiCollection.Name)]
public sealed class PaymentInstructionsEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Route = "/api/finance/payment-instructions";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    public static TheoryData<string> Methods => new() { "GET", "PUT" };

    private static JsonObject Pix(string kind = "cnpj", string? key = "12.345.678/0001-95") => new() { ["kind"] = kind, ["key"] = key };

    private static JsonObject Account(string? bank = "Banco do Brasil", string? agency = "1234-5", string? number = "12345-6", string? kind = "checking") =>
        new() { ["bank"] = bank, ["agency"] = agency, ["number"] = number, ["kind"] = kind };

    private static JsonObject Body(JsonObject? pix, JsonObject? account) => new() { ["pix"] = pix, ["account"] = account };

    [Theory]
    [MemberData(nameof(Methods))]
    public async Task Every_route_requires_a_token(string method)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), Route, null, method == "GET" ? null : new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Methods))]
    public async Task A_resident_is_refused(string method)
    {
        await MigrateAsync();
        Urban.Identity.Domain.Users.User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: Urban.Identity.Domain.Access.UserRole.Resident);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await _client.SendJsonAsync(new HttpMethod(method), Route, token, method == "GET" ? null : new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    [Fact]
    public async Task Get_before_anything_is_registered_answers_empty_and_never_caches()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, Route, manager.AccessToken);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        body.GetProperty("pix").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("account").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("updatedAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Put_saves_the_key_and_the_account_normalized_and_get_returns_them()
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage saved = await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(Pix(), Account("  Banco   do Brasil ", " 1234-5 ", "12345-6", "savings")));
        JsonElement read = await (await _client.SendJsonAsync(HttpMethod.Get, Route, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();
        JsonElement answer = await saved.Content.ReadFromJsonAsync<JsonElement>();

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        saved.Headers.CacheControl!.NoStore.Should().BeTrue();
        foreach (JsonElement body in new[] { answer, read })
        {
            body.GetProperty("pix").GetProperty("kind").GetString().Should().Be("cnpj");
            body.GetProperty("pix").GetProperty("key").GetString().Should().Be("12345678000195");
            body.GetProperty("account").GetProperty("bank").GetString().Should().Be("Banco do Brasil");
            body.GetProperty("account").GetProperty("agency").GetString().Should().Be("1234-5");
            body.GetProperty("account").GetProperty("number").GetString().Should().Be("12345-6");
            body.GetProperty("account").GetProperty("kind").GetString().Should().Be("savings");
            body.GetProperty("updatedAt").GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        }
    }

    [Theory]
    [InlineData("cnpj", "12.345.678/0001-95", "12345678000195")]
    [InlineData("cpf", "123.456.789-09", "12345678909")]
    [InlineData("email", "Financeiro@Condominio.com", "financeiro@condominio.com")]
    [InlineData("phone", "(11) 91234-5678", "+5511912345678")]
    [InlineData("random", "123E4567-E89B-12D3-A456-426614174000", "123e4567-e89b-12d3-a456-426614174000")]
    public async Task Put_accepts_every_kind_of_key(string kind, string typed, string stored)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(Pix(kind, typed), null));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement pix = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pix");
        pix.GetProperty("kind").GetString().Should().Be(kind);
        pix.GetProperty("key").GetString().Should().Be(stored);
    }

    [Fact]
    public async Task Put_with_only_the_key_or_only_the_account_leaves_the_other_empty()
    {
        ManagerSession manager = await RegisterAsync();

        JsonElement onlyKey = await (await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(Pix(), null))).Content.ReadFromJsonAsync<JsonElement>();
        JsonElement onlyAccount = await (await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(null, Account()))).Content.ReadFromJsonAsync<JsonElement>();

        onlyKey.GetProperty("account").ValueKind.Should().Be(JsonValueKind.Null);
        onlyAccount.GetProperty("pix").ValueKind.Should().Be(JsonValueKind.Null);
        onlyAccount.GetProperty("account").GetProperty("bank").GetString().Should().Be("Banco do Brasil");
    }

    [Fact]
    public async Task Put_with_nothing_clears_what_was_registered()
    {
        ManagerSession manager = await RegisterAsync();
        await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(Pix(), Account()));

        HttpResponseMessage cleared = await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, new JsonObject());
        JsonElement read = await (await _client.SendJsonAsync(HttpMethod.Get, Route, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();

        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        read.GetProperty("pix").ValueKind.Should().Be(JsonValueKind.Null);
        read.GetProperty("account").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("random-kind", "12345678909", "finance.payment_instructions.pix_kind_invalid")]
    [InlineData(null, "12345678909", "finance.payment_instructions.pix_kind_invalid")]
    [InlineData("cpf", "123", "finance.payment_instructions.pix_key_invalid")]
    [InlineData("cpf", null, "finance.payment_instructions.pix_key_invalid")]
    [InlineData("phone", "(11) 3123-4567", "finance.payment_instructions.pix_key_invalid")]
    public async Task Put_with_an_invalid_key_answers_400_with_the_code_of_the_problem(string? kind, string? key, string code)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(Pix(kind!, key), null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Theory]
    [InlineData("", "1234-5", "12345-6", "checking", "finance.payment_instructions.bank_invalid")]
    [InlineData("Itaú", "abcd", "12345-6", "checking", "finance.payment_instructions.agency_invalid")]
    [InlineData("Itaú", "1234-5", "abc", "checking", "finance.payment_instructions.account_number_invalid")]
    [InlineData("Itaú", "1234-5", "12345-6", "current", "finance.payment_instructions.account_kind_invalid")]
    [InlineData("Itaú", "1234-5", "12345-6", null, "finance.payment_instructions.account_kind_invalid")]
    public async Task Put_with_an_invalid_account_answers_400_with_the_code_of_the_problem(string bank, string agency, string number, string? kind, string code)
    {
        ManagerSession manager = await RegisterAsync();

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(null, Account(bank, agency, number, kind)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
    }

    [Fact]
    public async Task A_refused_put_keeps_what_was_registered()
    {
        ManagerSession manager = await RegisterAsync();
        await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(Pix(), Account()));

        await _client.SendJsonAsync(HttpMethod.Put, Route, manager.AccessToken, Body(Pix("cpf", "123"), null));
        JsonElement read = await (await _client.SendJsonAsync(HttpMethod.Get, Route, manager.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();

        read.GetProperty("pix").GetProperty("key").GetString().Should().Be("12345678000195");
        read.GetProperty("account").GetProperty("bank").GetString().Should().Be("Banco do Brasil");
    }

    [Fact]
    public async Task One_condominium_never_sees_the_instructions_of_another()
    {
        ManagerSession first = await RegisterAsync();
        await _client.SendJsonAsync(HttpMethod.Put, Route, first.AccessToken, Body(Pix(), Account()));
        ManagerSession second = await ManagerSetup.RegisterAsync(fixture, _client);

        JsonElement read = await (await _client.SendJsonAsync(HttpMethod.Get, Route, second.AccessToken)).Content.ReadFromJsonAsync<JsonElement>();

        read.GetProperty("pix").ValueKind.Should().Be(JsonValueKind.Null);
        read.GetProperty("account").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private async Task MigrateAsync() => await fixture.Factory.Services.MigrateFinanceAsync();

    private async Task<ManagerSession> RegisterAsync()
    {
        await MigrateAsync();

        return await ManagerSetup.RegisterAsync(fixture, _client);
    }
}
