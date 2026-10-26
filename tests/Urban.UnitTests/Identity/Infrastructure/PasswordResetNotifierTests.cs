using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class PasswordResetNotifierTests
{
    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<PasswordResetNotifier> _logger = new();

    [Fact]
    public async Task SendAsync_SendsAPortugueseMessageWithTheResetLink()
    {
        Capture();
        PasswordResetNotifier notifier = Create("https://urban-pro.io");

        await notifier.SendAsync(Email.Create("sindica@example.com").Value, "segredo", default);

        EmailMessage sent = _captured!;
        sent.To.Should().Be("sindica@example.com");
        sent.Subject.Should().Be("Redefina sua senha no Urban");
        sent.TextBody.Should().Contain("https://urban-pro.io/redefinir-senha?token=segredo");
        sent.TextBody.Should().Contain("60 minutos").And.Contain("uma vez").And.Contain("ignore esta mensagem");
    }

    [Fact]
    public async Task SendAsync_EscapesTheTokenInTheLink()
    {
        Capture();
        PasswordResetNotifier notifier = Create("https://urban-pro.io/");

        await notifier.SendAsync(Email.Create("sindica@example.com").Value, "a+b/c=d&e", default);

        _captured!.TextBody.Should().Contain("/redefinir-senha?token=a%2Bb%2Fc%3Dd%26e");
        _captured.TextBody.Should().NotContain("//redefinir-senha");
    }

    [Fact]
    public async Task SendAsync_KeepsTheSitePathWhenTheBaseAddressHasOne()
    {
        Capture();
        PasswordResetNotifier notifier = Create("https://exemplo.com/app");

        await notifier.SendAsync(Email.Create("sindica@example.com").Value, "t", default);

        _captured!.TextBody.Should().Contain("https://exemplo.com/app/redefinir-senha?token=t");
    }

    [Fact]
    public async Task SendAsync_WhenTheProviderFails_LogsAnErrorWithoutTheAddressOrTheLinkAndRethrowsForTheRetry()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio (HTTP 401, código unauthorized).");
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        PasswordResetNotifier notifier = Create("https://urban-pro.io");

        Func<Task> send = () => notifier.SendAsync(Email.Create("sindica@example.com").Value, "segredo", default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Should().BeSameAs(failure);
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
        PasswordResetNotifier notifier = Create("https://urban-pro.io");

        Func<Task> send = () => notifier.SendAsync(Email.Create("sindica@example.com").Value, "segredo", default);

        await send.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().BeEmpty();
    }

    private EmailMessage? _captured;

    private void Capture() =>
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _captured = m);

    private PasswordResetNotifier Create(string baseUrl) => new(_sender.Object, new WebLinksOptions { WebBaseUrl = baseUrl }, _logger);
}