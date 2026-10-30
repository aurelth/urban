using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class SecurityNoticeNotifierTests
{
    private static readonly Email Current = Email.Create("sindica@example.com").Value;
    private static readonly Email Other = Email.Create("nova@example.com").Value;

    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<SecurityNoticeNotifier> _logger = new();
    private EmailMessage? _captured;

    [Fact]
    public async Task PasswordChanged_TellsThePersonAndPointsToTheRecoveryScreen()
    {
        Capture();

        await Create().SendAsync(SecurityNotice.PasswordChanged, Current, null, default);

        _captured!.To.Should().Be("sindica@example.com");
        _captured.Subject.Should().Be("A senha da sua conta no Urban foi alterada");
        _captured.TextBody.Should().Contain("senha").And.Contain("https://urban-pro.io/esqueci-a-senha");
        _captured.TextBody.Should().NotContain("Sua senha é");
    }

    [Fact]
    public async Task EmailChangeRequested_GoesToTheCurrentAddress_AndNamesTheRequestedOne()
    {
        Capture();

        await Create().SendAsync(SecurityNotice.EmailChangeRequested, Current, Other, default);

        _captured!.To.Should().Be("sindica@example.com");
        _captured.Subject.Should().Be("Pedido para trocar o e-mail da sua conta no Urban");
        _captured.TextBody.Should().Contain("nova@example.com").And.Contain("continua valendo");
    }

    [Fact]
    public async Task EmailChanged_GoesToThePreviousAddress_AndNamesTheNewOne()
    {
        Capture();

        await Create().SendAsync(SecurityNotice.EmailChanged, Current, Other, default);

        _captured!.To.Should().Be("sindica@example.com");
        _captured.Subject.Should().Be("O e-mail da sua conta no Urban foi alterado");
        _captured.TextBody.Should().Contain("nova@example.com").And.Contain("https://urban-pro.io/esqueci-a-senha");
    }

    [Fact]
    public async Task WhenTheProviderFails_LogsAnErrorWithoutTheAddressesAndRethrowsForTheRetry()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio (HTTP 401, código unauthorized).");
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        Func<Task> send = () => Create().SendAsync(SecurityNotice.EmailChanged, Current, Other, default);

        (await send.Should().ThrowAsync<EmailDeliveryException>()).Which.Should().BeSameAs(failure);
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().Contain("example.com").And.NotContain("sindica@").And.NotContain("nova@");
    }

    [Fact]
    public async Task DoesNotHideFailuresThatAreNotDeliveryErrors()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));

        Func<Task> send = () => Create().SendAsync(SecurityNotice.PasswordChanged, Current, null, default);

        await send.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task AKindThatNeedsTheOtherAddress_WithoutIt_Fails()
    {
        Func<Task> send = () => Create().SendAsync(SecurityNotice.EmailChanged, Current, null, default);

        await send.Should().ThrowAsync<ArgumentNullException>();
        _sender.VerifyNoOtherCalls();
    }

    private void Capture() =>
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _captured = m);

    private SecurityNoticeNotifier Create() =>
        new(_sender.Object, new WebLinksOptions { WebBaseUrl = "https://urban-pro.io" }, _logger);
}
