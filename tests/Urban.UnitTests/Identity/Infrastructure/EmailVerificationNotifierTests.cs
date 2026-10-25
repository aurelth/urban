using FluentAssertions;
using Moq;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Identity.Infrastructure;

public class EmailVerificationNotifierTests
{
    private readonly Mock<IEmailSender> _sender = new();

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

    private EmailVerificationNotifier Create(string baseUrl) => new(_sender.Object, new WebLinksOptions { WebBaseUrl = baseUrl });
}