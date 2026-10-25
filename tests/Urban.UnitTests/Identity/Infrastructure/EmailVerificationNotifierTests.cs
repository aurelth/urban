using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class EmailVerificationNotifierTests
{
    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<EmailVerificationNotifier> _logger = new();

    [Fact]
    public async Task SendAsync_SendsAPortugueseMessageWithTheConfirmationLink()
    {
        EmailMessage? sent = null;
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => sent = m);
        EmailVerificationNotifier notifier = Create("https://urban-pro.io");

        await notifier.SendAsync(Email.Create("sindica@example.com").Value, "segredo", default);

        sent!.To.Should().Be("sindica@example.com");
        sent.Subject.Should().Be("Confirme seu e-mail no Urban");
        sent.TextBody.Should().Contain("https://urban-pro.io/confirmar-email?token=segredo");
        sent.TextBody.Should().Contain("24 horas").And.Contain("uma vez");
    }

    [Fact]
    public async Task SendAsync_EscapesTheTokenInTheLink()
    {
        EmailMessage? sent = null;
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => sent = m);
        EmailVerificationNotifier notifier = Create("https://urban-pro.io/");

        await notifier.SendAsync(Email.Create("sindica@example.com").Value, "a+b/c=d&e", default);

        sent!.TextBody.Should().Contain("/confirmar-email?token=a%2Bb%2Fc%3Dd%26e");
        sent.TextBody.Should().NotContain("//confirmar-email");
    }

    [Fact]
    public async Task SendAsync_KeepsTheSitePathWhenTheBaseAddressHasOne()
    {
        EmailMessage? sent = null;
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => sent = m);
        EmailVerificationNotifier notifier = Create("https://exemplo.com/app");

        await notifier.SendAsync(Email.Create("sindica@example.com").Value, "t", default);

        sent!.TextBody.Should().Contain("https://exemplo.com/app/confirmar-email?token=t");
    }

    [Fact]
    public async Task SendAsync_WhenTheProviderFails_LogsAnErrorWithoutTheAddressOrTheLinkAndDoesNotThrow()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio (HTTP 401, código unauthorized).");
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        EmailVerificationNotifier notifier = Create("https://urban-pro.io");

        Func<Task> send = () => notifier.SendAsync(Email.Create("sindica@example.com").Value, "segredo", default);

        await send.Should().NotThrowAsync();
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().Contain("example.com").And.NotContain("sindica").And.NotContain("segredo");
    }

    [Fact]
    public async Task SendAsync_DoesNotHideFailuresThatAreNotDeliveryErrors()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));
        EmailVerificationNotifier notifier = Create("https://urban-pro.io");

        Func<Task> send = () => notifier.SendAsync(Email.Create("sindica@example.com").Value, "segredo", default);

        await send.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().BeEmpty();
    }

    private EmailVerificationNotifier Create(string baseUrl) => new(_sender.Object, new WebLinksOptions { WebBaseUrl = baseUrl }, _logger);
}