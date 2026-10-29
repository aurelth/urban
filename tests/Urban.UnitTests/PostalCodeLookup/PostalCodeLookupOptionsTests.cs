using FluentAssertions;
using Urban.PostalCodeLookup;

namespace Urban.UnitTests.PostalCodeLookup;

public class PostalCodeLookupOptionsTests
{
    private static readonly PostalCodeProviderOptions Valid = new() { BaseUrl = "https://provedor.example/" };

    [Fact]
    public void Validate_AcceptsHttpsUrlsForBothProviders()
    {
        Action validate = () => Options(Valid, Valid).Validate();

        validate.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("nao-e-url")]
    [InlineData("/caminho/relativo")]
    [InlineData("http://brasilapi.com.br/")]
    [InlineData("ftp://brasilapi.com.br/")]
    public void Validate_RefusesABrasilApiUrlThatIsMissingOrNotHttps(string url)
    {
        Action validate = () => Options(new PostalCodeProviderOptions { BaseUrl = url }, Valid).Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage("*PostalCodeLookup:BrasilApi:BaseUrl*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-url")]
    [InlineData("http://viacep.com.br/")]
    public void Validate_RefusesAViaCepUrlThatIsMissingOrNotHttps(string url)
    {
        Action validate = () => Options(Valid, new PostalCodeProviderOptions { BaseUrl = url }).Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage("*PostalCodeLookup:ViaCep:BaseUrl*");
    }

    [Fact]
    public void Validate_NeverRepeatsTheValueItRefused()
    {
        Action validate = () => Options(new PostalCodeProviderOptions { BaseUrl = "http://segredo.example/" }, Valid).Validate();

        validate.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain("segredo");
    }

    [Fact]
    public void ANewInstanceHasNoUrlsSoAMissingSectionIsRefused()
    {
        Action validate = new PostalCodeLookupOptions().Validate;

        validate.Should().Throw<InvalidOperationException>().WithMessage("*PostalCodeLookup:BrasilApi:BaseUrl*");
    }

    private static PostalCodeLookupOptions Options(PostalCodeProviderOptions brasilApi, PostalCodeProviderOptions viaCep) =>
        new() { BrasilApi = brasilApi, ViaCep = viaCep };
}
