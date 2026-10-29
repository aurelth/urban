using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urban.PostalCodeLookup;

namespace Urban.IntegrationTests.Api;

public class PostalCodeLookupConfigurationTests
{
    [Fact]
    public void The_appsettings_ship_the_https_urls_of_both_providers()
    {
        using ApiFactory factory = new();

        PostalCodeLookupOptions options = factory.Services.GetRequiredService<PostalCodeLookupOptions>();

        options.BrasilApi.BaseUrl.Should().Be("https://brasilapi.com.br/");
        options.ViaCep.BaseUrl.Should().Be("https://viacep.com.br/");
    }

    [Theory]
    [InlineData("PostalCodeLookup:BrasilApi:BaseUrl", "")]
    [InlineData("PostalCodeLookup:BrasilApi:BaseUrl", "nao-e-url")]
    [InlineData("PostalCodeLookup:BrasilApi:BaseUrl", "http://brasilapi.com.br/")]
    [InlineData("PostalCodeLookup:ViaCep:BaseUrl", "")]
    [InlineData("PostalCodeLookup:ViaCep:BaseUrl", "nao-e-url")]
    [InlineData("PostalCodeLookup:ViaCep:BaseUrl", "http://viacep.com.br/")]
    public void Refuses_to_start_with_a_missing_or_invalid_provider_url(string key, string value)
    {
        using ApiFactory factory = new() { Settings = new Dictionary<string, string?> { [key] = value } };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }
}
