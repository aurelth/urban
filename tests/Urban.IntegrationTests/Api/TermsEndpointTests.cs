using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class TermsEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Current_terms_are_public_and_carry_only_the_configured_version()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/terms/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("version");
        body.GetProperty("version").GetString().Should().Be(fixture.Factory.TermsVersion);
    }

    [Fact]
    public async Task Current_terms_follow_the_configuration()
    {
        await using ApiFactory factory = new() { TermsVersion = "2027-02-01" };
        using HttpClient client = factory.CreateClient();

        JsonElement body = await client.GetFromJsonAsync<JsonElement>("/api/terms/current");

        body.GetProperty("version").GetString().Should().Be("2027-02-01");
    }

    [Fact]
    public async Task Current_terms_are_never_cached()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/terms/current");

        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task The_version_a_client_reads_is_the_one_the_sign_up_accepts()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string version = (await client.GetFromJsonAsync<JsonElement>("/api/terms/current")).GetProperty("version").GetString()!;

        version.Should().Be(fixture.Factory.TermsVersion);
        version.Length.Should().BeInRange(1, 32);
    }
}