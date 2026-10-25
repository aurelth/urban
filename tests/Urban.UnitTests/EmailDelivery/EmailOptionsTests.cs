using FluentAssertions;
using Urban.EmailDelivery;

namespace Urban.UnitTests.EmailDelivery;

public class EmailOptionsTests
{
    private const string ApiKey = "chave-secreta-de-teste";

    [Fact]
    public void Validate_AcceptsACompleteBrevoConfiguration()
    {
        Action validate = () => Valid().Validate();

        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_DoesNotNeedAKeyForTheLogProvider()
    {
        EmailOptions options = Valid(provider: EmailProvider.Log, apiKey: string.Empty);

        Action validate = () => options.Validate();

        validate.Should().NotThrow();
    }

    [Fact]
    public void Validate_RequiresAProvider()
    {
        EmailOptions options = Valid(provider: null);

        Action validate = () => options.Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage("*Email:Provider*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    [InlineData("Urban <nao-responda@urban-pro.io>")]
    public void Validate_RequiresAPlainSenderAddress(string address)
    {
        EmailOptions options = Valid(senderAddress: address);

        Action validate = () => options.Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage("*Email:SenderAddress*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RequiresASenderName(string name)
    {
        EmailOptions options = Valid(senderName: name);

        Action validate = () => options.Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage("*Email:SenderName*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RequiresTheBrevoKeyWhenTheProviderIsBrevo(string apiKey)
    {
        EmailOptions options = Valid(apiKey: apiKey);

        Action validate = () => options.Validate();

        validate.Should().Throw<InvalidOperationException>().WithMessage("*Email:Brevo:ApiKey*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("api.brevo.com")]
    [InlineData("http://api.brevo.com/")]
    public void Validate_RequiresAnAbsoluteHttpsBrevoAddress_AndNeverRepeatsTheKey(string baseUrl)
    {
        EmailOptions options = Valid(baseUrl: baseUrl);

        Action validate = () => options.Validate();

        validate.Should().Throw<InvalidOperationException>()
            .Where(e => e.Message.Contains("Email:Brevo:BaseUrl", StringComparison.Ordinal) && !e.Message.Contains(ApiKey, StringComparison.Ordinal));
    }

    private static EmailOptions Valid(
        EmailProvider? provider = EmailProvider.Brevo,
        string senderAddress = "nao-responda@urban-pro.io",
        string senderName = "Urban",
        string apiKey = ApiKey,
        string baseUrl = "https://api.brevo.com/") =>
        new()
        {
            Provider = provider,
            SenderAddress = senderAddress,
            SenderName = senderName,
            Brevo = new BrevoOptions { ApiKey = apiKey, BaseUrl = baseUrl },
        };
}