using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class ResidentInvitationNotifierTests
{
    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<ResidentInvitationNotifier> _logger = new();

    [Fact]
    public async Task SendAsync_SendsAnInvitationNamingTheCondominiumWithTheLinkAndTheValidity()
    {
        EmailMessage sent = await SendAsync("Residencial Vila Verde", "segredo", "https://urban-pro.io");

        sent.To.Should().Be("maria@exemplo.com");
        sent.Subject.Should().Be("Você foi convidado para o Urban");
        sent.TextBody.Should().Contain("Residencial Vila Verde");
        sent.TextBody.Should().Contain("https://urban-pro.io/confirmar-email?token=segredo");
        sent.TextBody.Should().Contain("72 horas").And.Contain("uma vez");
    }

    [Fact]
    public async Task SendAsync_WithoutTheCondominiumName_StillReadsWell()
    {
        EmailMessage sent = await SendAsync(null, "segredo", "https://urban-pro.io");

        sent.TextBody.Should().Contain("síndico do seu condomínio").And.NotContain("null");
    }

    [Fact]
    public async Task SendAsync_EscapesTheTokenInTheLink()
    {
        EmailMessage sent = await SendAsync("Vila", "a+b/c=d&e", "https://urban-pro.io/");

        sent.TextBody.Should().Contain("/confirmar-email?token=a%2Bb%2Fc%3Dd%26e").And.NotContain("//confirmar-email");
    }

    [Fact]
    public async Task SendAsync_WhenTheProviderFails_LogsAnErrorWithoutTheAddressOrTheLinkAndRethrowsForTheRetry()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio.");
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        ResidentInvitationNotifier notifier = Create("https://urban-pro.io");

        Func<Task> send = () => notifier.SendAsync(Email.Create("maria@exemplo.com").Value, "Vila", "segredo", default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Should().BeSameAs(failure);
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().Contain("exemplo.com").And.NotContain("maria").And.NotContain("segredo");
    }

    private async Task<EmailMessage> SendAsync(string? condominiumName, string token, string baseUrl)
    {
        EmailMessage? sent = null;
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => sent = m);

        await Create(baseUrl).SendAsync(Email.Create("maria@exemplo.com").Value, condominiumName, token, default);

        return sent!;
    }

    private ResidentInvitationNotifier Create(string baseUrl) => new(_sender.Object, new WebLinksOptions { WebBaseUrl = baseUrl }, _logger);
}
