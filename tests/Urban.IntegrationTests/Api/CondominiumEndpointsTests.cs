using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class CondominiumEndpointsTests(ApiFixture fixture) : IAsyncLifetime, IDisposable
{
    private const string Route = "/api/condominiums";
    private const string Password = "Senha-segura-123";
    private const string ManagerCpf = "52998224725";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public static TheoryData<string, JsonNode?, string> InvalidFields => new()
    {
        { "manager.fullName", "Ana", "condominium.person.full_name_invalid" },
        { "manager.cpf", "111.111.111-11", "condominium.cpf.invalid" },
        { "manager.birthDate", "2015-01-01", "condominium.birth_date.under_age" },
        { "manager.birthDate", null, "condominium.birth_date.invalid" },
        { "manager.phone", "123", "condominium.phone.invalid" },
        { "manager.phone", "(47) 3333-4444", "condominium.phone.mobile_required" },
        { "manager.address.postalCode", "12", "condominium.postal_code.invalid" },
        { "manager.address.state", "XX", "condominium.address.state_invalid" },
        { "manager.address.street", "", "condominium.address.street_invalid" },
        { "manager.address.number", null, "condominium.address.number_invalid" },
        { "manager.address.neighborhood", " ", "condominium.address.neighborhood_invalid" },
        { "manager.address.city", "", "condominium.address.city_invalid" },
        { "condominium.name", "A", "condominium.name.invalid" },
        { "condominium.cnpj", "123", "condominium.cnpj.invalid" },
        { "condominium.address.state", "ZZ", "condominium.address.state_invalid" },
        { "condominium.address.complement", new string('x', 61), "condominium.address.complement_invalid" },
        { "displayName", "1", "identity.display_name.invalid" },
        { "displayName", null, "identity.display_name.invalid" },
    };

    public Task InitializeAsync() => fixture.Factory.Services.MigrateCondominiumAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task A_manager_registers_the_condominium_and_gets_back_only_the_public_data()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        string cnpj = CondominiumBuilders.UniqueCnpj().Value;

        HttpResponseMessage response = await PostAsync(await SignInAsync(manager), Body(cnpj: cnpj));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        string raw = await response.Content.ReadAsStringAsync(Ct);
        JsonElement body = JsonDocument.Parse(raw).RootElement;
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("condominiumId", "name", "cnpj", "managerCpf");
        body.GetProperty("name").GetString().Should().Be("Residencial Vila Verde");
        body.GetProperty("cnpj").GetString().Should().Be(Formatted(cnpj));
        body.GetProperty("managerCpf").GetString().Should().Be("***.982.247-**");
        raw.Should().NotContain(ManagerCpf).And.NotContain("1985").And.NotContain("99123").And.NotContain("Rua XV");
    }

    [Fact]
    public async Task After_registering_a_refresh_brings_a_token_with_the_condominium_and_me_shows_the_name()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        HttpResponseMessage login = await _client.LoginAsync(manager.Email.Value, Password);
        string? cookie = login.RefreshCookieValue();
        HttpResponseMessage created = await PostAsync(await login.AccessTokenAsync(), Body());
        Guid condominiumId = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("condominiumId").GetGuid();

        HttpResponseMessage refreshed = await _client.RefreshAsync(cookie);
        HttpResponseMessage me = await _client.MeAsync(await refreshed.AccessTokenAsync());

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement current = await me.Content.ReadFromJsonAsync<JsonElement>(Ct);
        current.GetProperty("condominiumId").GetGuid().Should().Be(condominiumId);
        current.GetProperty("displayName").GetString().Should().Be("Ana");
    }

    [Fact]
    public async Task The_cpf_is_stored_encrypted_in_the_database()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        (await PostAsync(await SignInAsync(manager), Body())).StatusCode.Should().Be(HttpStatusCode.Created);

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        string stored = await db.Database
            .SqlQuery<string>($"""select cpf_encrypted as "Value" from condominium.person where user_id = {manager.Id}""").SingleAsync(Ct);

        stored.Should().StartWith("v1:").And.NotContain(ManagerCpf);
    }

    [Fact]
    public async Task Without_a_token_the_answer_is_401()
    {
        HttpResponseMessage response = await PostAsync(null, Body());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.CodeAsync()).Should().Be("identity.session.unauthenticated");
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Only_a_manager_can_register_a_condominium(UserRole role)
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(Password, role: role);

        HttpResponseMessage response = await PostAsync(await SignInAsync(user), Body());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
        (await CountCondominiumsOfAsync(user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_cnpj_already_registered_is_a_409_that_tells_to_ask_for_an_invitation()
    {
        User first = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        User second = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        string cnpj = CondominiumBuilders.UniqueCnpj().Value;
        (await PostAsync(await SignInAsync(first), Body(cnpj: cnpj))).StatusCode.Should().Be(HttpStatusCode.Created);
        string secondToken = await SignInAsync(second);

        HttpResponseMessage response = await PostAsync(secondToken, Body(cnpj: cnpj));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().Should().Be("condominium.cnpj.already_registered");
        problem.GetProperty("detail").GetString().Should().Be("Este CNPJ já está cadastrado. Peça um convite ao síndico atual");
        (await CountCondominiumsOfAsync(second.Id)).Should().Be(0);
        (await ReadUserAsync(second.Id)).CondominiumId.Should().BeNull();
    }

    [Fact]
    public async Task A_manager_who_already_registered_cannot_register_another()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        string token = await SignInAsync(manager);
        (await PostAsync(token, Body())).StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage response = await PostAsync(token, Body());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("condominium.manager.already_registered");
        (await CountCondominiumsOfAsync(manager.Id)).Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task An_invalid_field_is_a_400_with_the_code_of_that_field_and_stores_nothing(string path, JsonNode? value, string code)
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);

        HttpResponseMessage response = await PostAsync(await SignInAsync(manager), With(Body(), path, value));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(code);
        (await CountCondominiumsOfAsync(manager.Id)).Should().Be(0);
        (await ReadUserAsync(manager.Id)).CondominiumId.Should().BeNull();
    }

    [Fact]
    public async Task An_invalid_cpf_is_never_echoed_back()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);

        HttpResponseMessage response = await PostAsync(await SignInAsync(manager), With(Body(), "manager.cpf", "123.456.789-00"));

        string raw = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        raw.Should().NotContain("12345678900").And.NotContain("123.456.789");
    }

    [Fact]
    public async Task A_conflict_never_echoes_the_cpf_either()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        string token = await SignInAsync(manager);
        await PostAsync(token, Body());

        string raw = await (await PostAsync(token, Body())).Content.ReadAsStringAsync(Ct);

        raw.Should().NotContain(ManagerCpf);
    }

    [Fact]
    public async Task Empty_blocks_are_a_400_naming_the_first_missing_field()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);

        HttpResponseMessage response = await PostAsync(await SignInAsync(manager), new JsonObject());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.person.full_name_invalid");
    }

    [Theory]
    [InlineData("12/03/1985")]
    [InlineData("1985-13-45")]
    [InlineData("ontem")]
    public async Task A_birth_date_that_is_not_an_iso_date_is_a_400(string birthDate)
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);

        HttpResponseMessage response = await PostAsync(await SignInAsync(manager), With(Body(), "manager.birthDate", birthDate));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountCondominiumsOfAsync(manager.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_a_400()
    {
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        using HttpRequestMessage request = new(HttpMethod.Post, Route)
        {
            Content = new StringContent("{ isto nao e json", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(manager));

        HttpResponseMessage response = await _client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Each_condominium_sees_only_its_own_people()
    {
        User first = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        User second = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        string cpfOfFirst = CondominiumBuilders.UniqueCpf().Value;
        string cpfOfSecond = CondominiumBuilders.UniqueCpf().Value;
        Guid firstId = await RegisterAsync(first, cpfOfFirst);
        Guid secondId = await RegisterAsync(second, cpfOfSecond);

        Person[] seenByFirst = await PeopleSeenByAsync(firstId);
        Person[] seenBySecond = await PeopleSeenByAsync(secondId);
        Person[] seenByNobody = await PeopleSeenByAsync(null);
        Person[] seenByStranger = await PeopleSeenByAsync(Guid.CreateVersion7());

        seenByFirst.Should().ContainSingle().Which.UserId.Should().Be(first.Id);
        seenByFirst[0].Cpf.Value.Should().Be(cpfOfFirst);
        seenBySecond.Should().ContainSingle().Which.UserId.Should().Be(second.Id);
        seenBySecond[0].Cpf.Value.Should().Be(cpfOfSecond);
        seenByNobody.Should().BeEmpty("sem condomínio no token não se enxerga ninguém");
        seenByStranger.Should().BeEmpty();
    }

    [Fact]
    public async Task The_same_cpf_can_be_a_manager_of_two_different_condominiums_on_the_platform()
    {
        User first = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        User second = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        string cpf = CondominiumBuilders.UniqueCpf().Value;

        (await PostAsync(await SignInAsync(first), Body(cpf: cpf))).StatusCode.Should().Be(HttpStatusCode.Created);
        HttpResponseMessage response = await PostAsync(await SignInAsync(second), Body(cpf: cpf));

        response.StatusCode.Should().Be(HttpStatusCode.Created, "o CPF é único por condomínio, não na plataforma");
    }

    private static string Formatted(string cnpj) => $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}";

    private static JsonObject Body(string? cnpj = null, string? cpf = null) => new()
    {
        ["displayName"] = "Ana",
        ["manager"] = new JsonObject
        {
            ["fullName"] = "Ana Maria Silva",
            ["cpf"] = cpf ?? ManagerCpf,
            ["birthDate"] = "1985-03-12",
            ["phone"] = "47991234567",
            ["address"] = new JsonObject
            {
                ["postalCode"] = "89010000",
                ["street"] = "Rua XV de Novembro",
                ["number"] = "1234",
                ["complement"] = "Apto 5",
                ["neighborhood"] = "Centro",
                ["city"] = "Blumenau",
                ["state"] = "SC",
            },
        },
        ["condominium"] = new JsonObject
        {
            ["name"] = "Residencial Vila Verde",
            ["cnpj"] = cnpj ?? CondominiumBuilders.UniqueCnpj().Value,
            ["address"] = new JsonObject
            {
                ["postalCode"] = "89201000",
                ["street"] = "Rua Princesa Isabel",
                ["number"] = "88",
                ["neighborhood"] = "Atiradores",
                ["city"] = "Joinville",
                ["state"] = "SC",
            },
        },
    };

    private static JsonObject With(JsonObject body, string path, JsonNode? value)
    {
        string[] parts = path.Split('.');
        JsonObject target = body;
        foreach (string part in parts[..^1])
        {
            target = target[part]!.AsObject();
        }

        target[parts[^1]] = value;

        return body;
    }

    private Task<HttpResponseMessage> PostAsync(string? accessToken, JsonObject body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, Route) { Content = JsonContent.Create(body) };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return _client.SendAsync(request, Ct);
    }

    private async Task<string> SignInAsync(User user) =>
        await (await _client.LoginAsync(user.Email.Value, Password)).AccessTokenAsync();

    private async Task<Guid> RegisterAsync(User manager, string cpf)
    {
        HttpResponseMessage response = await PostAsync(await SignInAsync(manager), Body(cpf: cpf));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("condominiumId").GetGuid();
    }

    private async Task<Person[]> PeopleSeenByAsync(Guid? tenant)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        IPersonalDataProtector protector = scope.ServiceProvider.GetRequiredService<IPersonalDataProtector>();
        await using CondominiumDbContext db = new(
            CondominiumPersistence.CreateOptions(fixture.Postgres.ConnectionString),
            protector,
            new FixedTenant(tenant));

        return await db.People.AsNoTracking().ToArrayAsync(Ct);
    }

    private async Task<User> ReadUserAsync(Guid id)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();

        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
    }

    private async Task<int> CountCondominiumsOfAsync(Guid managerUserId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();

        return await db.Database
            .SqlQuery<int>($"""select count(*)::int as "Value" from condominium.condominium where created_by = {managerUserId}""").SingleAsync(Ct);
    }
}
