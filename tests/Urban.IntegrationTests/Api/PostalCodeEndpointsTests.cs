using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class PostalCodeEndpointsTests : IDisposable
{
    private const string Password = "Senha-segura-123";

    private static readonly PostalCodeAddress Paulista = new("01310100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP");

    private readonly ApiFixture _fixture;
    private readonly HttpClient _client;

    public PostalCodeEndpointsTests(ApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient(new() { HandleCookies = false });
        fixture.Factory.PostalCodes.Reset();
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Without_a_session_the_lookup_answers_401_and_never_reaches_the_providers()
    {
        _fixture.Factory.PostalCodes.Result = PostalCodeLookupResult.Found(Paulista);

        HttpResponseMessage response = await GetAsync("01310100", accessToken: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _fixture.Factory.PostalCodes.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task A_signed_in_user_gets_the_address_of_the_cep()
    {
        _fixture.Factory.PostalCodes.Result = PostalCodeLookupResult.Found(Paulista);

        HttpResponseMessage response = await GetAsync("01310100", await SignInAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("postalCode", "street", "neighborhood", "city", "state");
        body.GetProperty("postalCode").GetString().Should().Be("01310100");
        body.GetProperty("street").GetString().Should().Be("Avenida Paulista");
        body.GetProperty("neighborhood").GetString().Should().Be("Bela Vista");
        body.GetProperty("city").GetString().Should().Be("São Paulo");
        body.GetProperty("state").GetString().Should().Be("SP");
    }

    [Theory]
    [InlineData("01310-100")]
    [InlineData("01.310-100")]
    public async Task The_cep_may_come_with_punctuation_and_the_providers_receive_only_the_digits(string cep)
    {
        _fixture.Factory.PostalCodes.Result = PostalCodeLookupResult.Found(Paulista);

        HttpResponseMessage response = await GetAsync(cep, await SignInAsync());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _fixture.Factory.PostalCodes.Requested.Should().Equal("01310100");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    [InlineData("00000000")]
    [InlineData("013101000")]
    public async Task An_invalid_cep_answers_400_and_never_reaches_the_providers(string cep)
    {
        HttpResponseMessage response = await GetAsync(cep, await SignInAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("condominium.postal_code.invalid");
        _fixture.Factory.PostalCodes.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task A_cep_that_does_not_exist_answers_404()
    {
        _fixture.Factory.PostalCodes.Result = PostalCodeLookupResult.NotFound;

        HttpResponseMessage response = await GetAsync("99999999", await SignInAsync());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await response.CodeAsync()).Should().Be("condominium.postal_code.not_found");
    }

    [Fact]
    public async Task When_the_providers_are_down_the_lookup_answers_503()
    {
        _fixture.Factory.PostalCodes.Result = PostalCodeLookupResult.Unavailable;

        HttpResponseMessage response = await GetAsync("01310100", await SignInAsync());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.CodeAsync()).Should().Be("condominium.postal_code.lookup_unavailable");
    }

    private async Task<string> SignInAsync()
    {
        Urban.Identity.Domain.Users.User manager = await _fixture.Postgres.SeedManagerWithoutCondominiumWithPasswordAsync(Password);
        return await (await _client.LoginAsync(manager.Email.Value, Password)).AccessTokenAsync();
    }

    private Task<HttpResponseMessage> GetAsync(string cep, string? accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"/api/postal-codes/{cep}");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return _client.SendAsync(request);
    }
}
