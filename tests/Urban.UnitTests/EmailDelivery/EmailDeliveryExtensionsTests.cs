using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Urban.EmailDelivery;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.EmailDelivery;

public class EmailDeliveryExtensionsTests
{
    [Fact]
    public void AddEmailDelivery_WithTheBrevoProvider_RegistersTheBrevoSenderWithABoundedClient()
    {
        using ServiceProvider provider = Build(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Brevo",
            ["Email:Brevo:ApiKey"] = "chave",
        });

        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEmailSender>().Should().BeOfType<BrevoEmailSender>();

        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IEmailSender));
        client.BaseAddress.Should().Be("https://api.brevo.com/");
        client.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        client.MaxResponseContentBufferSize.Should().Be(64 * 1024);
    }

    [Fact]
    public void AddEmailDelivery_WithTheLogProvider_RegistersTheLogOnlySender()
    {
        using ServiceProvider provider = Build(new Dictionary<string, string?> { ["Email:Provider"] = "Log" });

        provider.GetRequiredService<IEmailSender>().Should().BeOfType<LogOnlyEmailSender>();
    }

    [Fact]
    public void AddEmailDelivery_ReadsTheSenderFromTheConfiguration()
    {
        using ServiceProvider provider = Build(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Log",
            ["Email:SenderAddress"] = "outro@urban-pro.io",
            ["Email:SenderName"] = "Outro",
        });

        EmailOptions options = provider.GetRequiredService<EmailOptions>();

        options.SenderAddress.Should().Be("outro@urban-pro.io");
        options.SenderName.Should().Be("Outro");
    }

    [Fact]
    public void AddEmailDelivery_RefusesAnIncompleteConfiguration()
    {
        Action build = () => Build(new Dictionary<string, string?> { ["Email:Provider"] = "Brevo" });

        build.Should().Throw<InvalidOperationException>().WithMessage("*Email:Brevo:ApiKey*");
    }

    [Fact]
    public void AddEmailDelivery_RefusesAMissingSection()
    {
        Action build = () => Build([]);

        build.Should().Throw<InvalidOperationException>().WithMessage("*Email:Provider*");
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        settings.TryAdd("Email:SenderAddress", "nao-responda@urban-pro.io");
        settings.TryAdd("Email:SenderName", "Urban");
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddEmailDelivery(configuration);
        return services.BuildServiceProvider();
    }
}