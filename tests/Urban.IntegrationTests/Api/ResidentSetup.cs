using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>O que os testes de ponta a ponta repetem para ter um morador de verdade: unidade, cadastro, convite, senha escolhida pelo link e entrada.</summary>
internal static class ResidentSetup
{
    private const string Residents = "/api/residents";

    public static async Task<Guid> CreateUnitAsync(HttpClient client, ManagerSession manager, string number)
    {
        HttpResponseMessage response = await client.SendJsonAsync(HttpMethod.Post, "/api/units", manager.AccessToken, new JsonObject { ["number"] = number, ["type"] = "apartment" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Cadastra o morador na unidade, convida, ele escolhe a senha pelo link do e-mail e entra: o token é o de quem mora de verdade.</summary>
    public static async Task<string> TokenAsync(ApiFixture fixture, HttpClient client, ManagerSession manager, Guid unitId, string kind = "owner", string fullName = "Maria Souza") =>
        (await AccountAsync(fixture, client, manager, unitId, kind, fullName)).Token;

    /// <summary>Como <see cref="TokenAsync"/>, mas devolve também o e-mail de login do morador, para o teste ver o que chegou a ele.</summary>
    public static async Task<(string Token, string Email)> AccountAsync(ApiFixture fixture, HttpClient client, ManagerSession manager, Guid unitId, string kind = "owner", string fullName = "Maria Souza")
    {
        string email = $"morador-{Guid.NewGuid():N}@exemplo.com";
        JsonObject body = new()
        {
            ["fullName"] = fullName,
            ["cpf"] = CondominiumBuilders.UniqueCpf().Value,
            ["birthDate"] = "1990-05-20",
            ["phone"] = "(47) 99876-5432",
            ["email"] = email,
            ["profession"] = "Engenheira",
            ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
            ["unitId"] = unitId,
            ["kind"] = kind,
            ["startDate"] = "2024-03-01",
        };
        HttpResponseMessage created = await client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        Guid residentId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        HttpResponseMessage invited = await client.SendJsonAsync(HttpMethod.Post, $"{Residents}/{residentId}/invitation", manager.AccessToken);
        invited.StatusCode.Should().Be(HttpStatusCode.Accepted, await invited.Content.ReadAsStringAsync());
        string link = RecordingEmailSender.TokenIn(fixture.Factory.Emails.SentTo(email).Last());
        HttpResponseMessage confirmed = await client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new { token = link, password = ManagerSetup.Password, termsVersion = fixture.Factory.TermsVersion });
        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent, await confirmed.Content.ReadAsStringAsync());

        return (await (await client.LoginAsync(email, ManagerSetup.Password)).AccessTokenAsync(), email);
    }
}
