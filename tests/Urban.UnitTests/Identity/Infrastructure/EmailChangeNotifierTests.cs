using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class EmailChangeNotifierTests
{
    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<EmailChangeNotifier> _logger = new();
    private EmailMessage? _captured;

    [Fact]
    public async Task SendLinkAsync_SendsAPortugueseMessageToTheNewAddressWithTheConfirmationLink()
    {
        Capture();
        EmailChangeNotifier notifier = Create("https://urban-pro.io");

        await notifier.SendLinkAsync(Email.Create("nova@example.com").Value, "segredo", default);

        EmailMessage sent = _captured!;
        sent.To.Should().Be("nova@example.com");
        sent.Subject.Should().Be("Confirme o novo e-mail da sua conta no Urban");
        sent.TextBody.Should().Contain("https://urban-pro.io/confirmar-troca-de-email?token=segredo");
        sent.TextBody.Should().Contain("24 horas").And.Contain("uma vez").And.Contain("ignore esta mensagem");
    }

    [Fact]
    public async Task SendLinkAsync_EscapesTheTokenInTheLink()
    {
        Capture();
        EmailChangeNotifier notifier = Create("https://urban-pro.io/");

        await notifier.SendLinkAsync(Email.Create("nova@example.com").Value, "a+b/c=d&e", default);

        _captured!.TextBody.Should().Contain("/confirmar-troca-de-email?token=a%2Bb%2Fc%3Dd%26e");
        _captured.TextBody.Should().NotContain("//confirmar-troca-de-email");
    }

    [Fact]
    public async Task SendLinkAsync_WhenTheProviderFails_LogsAnErrorWithoutTheAddressOrTheLinkAndRethrowsForTheRetry()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio (HTTP 401, código unauthorized).");
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        EmailChangeNotifier notifier = Create("https://urban-pro.io");

        Func<Task> send = () => notifier.SendLinkAsync(Email.Create("nova@example.com").Value, "segredo", default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Should().BeSameAs(failure);
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().Contain("example.com").And.NotContain("nova@").And.NotContain("segredo");
    }

    [Fact]
    public async Task SendLinkAsync_DoesNotHideFailuresThatAreNotDeliveryErrors()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));
        EmailChangeNotifier notifier = Create("https://urban-pro.io");

        Func<Task> send = () => notifier.SendLinkAsync(Email.Create("nova@example.com").Value, "segredo", default);

        await send.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().BeEmpty();
    }

    private void Capture() =>
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _captured = m);

    private EmailChangeNotifier Create(string baseUrl) => new(_sender.Object, new WebLinksOptions { WebBaseUrl = baseUrl }, _logger);
}
