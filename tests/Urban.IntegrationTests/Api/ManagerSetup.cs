using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.Condominium.Infrastructure;
using Urban.Identity.Domain.Users;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>Um síndico que já entrou e já cadastrou o condomínio, pronto para os testes das telas "Meus dados".</summary>
internal sealed record ManagerSession(User User, string AccessToken, Guid CondominiumId);

internal static class ManagerSetup
{
    public const string Password = "Senha-segura-123";
    public const string Cpf = "52998224725";
    public const string MineRoute = "/api/condominiums/mine";
    public const string IdentityRoute = "/api/condominiums/mine/manager-identity";
    public const string ChangePasswordRoute = "/api/auth/change-password";
    public const string ChangeEmailRoute = "/api/auth/change-email";
    public const string ConfirmEmailChangeRoute = "/api/auth/confirm-email-change";

    /// <summary>Cadastra o síndico e o condomínio pela API, como a tela faz, e devolve um token que já carrega o condomínio.</summary>
    public static async Task<ManagerSession> RegisterAsync(ApiFixture fixture, HttpClient client)
    {
        await fixture.Factory.Services.MigrateCondominiumAsync();
        User manager = await fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        HttpResponseMessage login = await client.LoginAsync(manager.Email.Value, Password);
        string? cookie = login.RefreshCookieValue();

        HttpResponseMessage created = await client.SendJsonAsync(HttpMethod.Post, "/api/condominiums", await login.AccessTokenAsync(), RegistrationBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid condominiumId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("condominiumId").GetGuid();

        HttpResponseMessage refreshed = await client.RefreshAsync(cookie);

        return new ManagerSession(manager, await refreshed.AccessTokenAsync(), condominiumId);
    }

    public static async Task<string> SignInAsync(this HttpClient client, User user, string password = Password) =>
        await (await client.LoginAsync(user.Email.Value, password)).AccessTokenAsync();

    public static Task<HttpResponseMessage> SendJsonAsync(
        this HttpClient client,
        HttpMethod method,
        string route,
        string? accessToken,
        object? body = null)
    {
        HttpRequestMessage request = new(method, route);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadMineAsync(this HttpClient client, string accessToken)
    {
        HttpResponseMessage response = await client.SendJsonAsync(HttpMethod.Get, MineRoute, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Corpo do PUT dos campos livres: só os campos que a tela deixa editar.</summary>
    public static JsonObject FreeFieldsBody(string version, string displayName = "Ana", string phone = "47991234567", string condominiumName = "Residencial Vila Verde") => new()
    {
        ["version"] = version,
        ["displayName"] = displayName,
        ["phone"] = phone,
        ["managerAddress"] = Address("89010000", "Rua XV de Novembro", "1234", "Apto 5", "Centro", "Blumenau"),
        ["condominiumName"] = condominiumName,
        ["condominiumAddress"] = Address("89201000", "Rua Princesa Isabel", "88", null, "Atiradores", "Joinville"),
    };

    public static JsonObject Address(string postalCode, string street, string number, string? complement, string neighborhood, string city) => new()
    {
        ["postalCode"] = postalCode,
        ["street"] = street,
        ["number"] = number,
        ["complement"] = complement,
        ["neighborhood"] = neighborhood,
        ["city"] = city,
        ["state"] = "SC",
    };

    private static JsonObject RegistrationBody() => new()
    {
        ["displayName"] = "Ana",
        ["manager"] = new JsonObject
        {
            ["fullName"] = "Ana Maria Silva",
            ["cpf"] = Cpf,
            ["birthDate"] = "1985-03-12",
            ["phone"] = "47991234567",
            ["address"] = Address("89010000", "Rua XV de Novembro", "1234", "Apto 5", "Centro", "Blumenau"),
        },
        ["condominium"] = new JsonObject
        {
            ["name"] = "Residencial Vila Verde",
            ["cnpj"] = CondominiumBuilders.UniqueCnpj().Value,
            ["address"] = Address("89201000", "Rua Princesa Isabel", "88", null, "Atiradores", "Joinville"),
        },
    };
}
