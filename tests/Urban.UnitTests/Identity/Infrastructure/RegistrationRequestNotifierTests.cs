using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class RegistrationRequestNotifierTests
{
    private static readonly RegistrationRequestSummary Request = new(
        Guid.CreateVersion7(),
        "ana@example.com",
        "Ana Paula",
        "Residencial Vila Verde",
        new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero));

    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<RegistrationRequestNotifier> _logger = new();

    private EmailMessage? _captured;

    [Fact]
    public async Task NotifyAsync_SendsAPortugueseMessageToTheOwnerWithTheRequestAndTheReviewLink()
    {
        Capture();
        RegistrationRequestNotifier notifier = Create("dono@example.com", "https://urban-pro.io");

        await notifier.NotifyAsync(Request, default);

        EmailMessage sent = _captured!;
        sent.To.Should().Be("dono@example.com");
        sent.Subject.Should().Be("Novo pedido de cadastro no Urban");
        sent.TextBody.Should().Contain("Ana Paula").And.Contain("ana@example.com").And.Contain("Residencial Vila Verde");
        sent.TextBody.Should().Contain("https://urban-pro.io/admin/pedidos-de-cadastro");
    }

    [Fact]
    public async Task NotifyAsync_KeepsTheSitePathWhenTheBaseAddressHasOne()
    {
        Capture();
        RegistrationRequestNotifier notifier = Create("dono@example.com", "https://exemplo.com/app/");

        await notifier.NotifyAsync(Request, default);

        _captured!.TextBody.Should().Contain("https://exemplo.com/app/admin/pedidos-de-cadastro");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NotifyAsync_WithoutAConfiguredOwnerAddress_SendsNothingAndLogsAWarning(string address)
    {
        RegistrationRequestNotifier notifier = Create(address, "https://urban-pro.io");

        await notifier.NotifyAsync(Request, default);

        _sender.VerifyNoOtherCalls();
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().NotContain("ana@example.com").And.NotContain("Ana Paula");
    }

    [Fact]
    public async Task NotifyAsync_WhenTheProviderFails_LogsAnErrorWithoutPersonalDataAndRethrowsForTheRetry()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio (HTTP 401, código unauthorized).");
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        RegistrationRequestNotifier notifier = Create("dono@example.com", "https://urban-pro.io");

        Func<Task> notify = () => notifier.NotifyAsync(Request, default);

        (await notify.Should().ThrowAsync<EmailDeliveryException>()).Which.Should().BeSameAs(failure);
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().NotContain("ana@example.com").And.NotContain("Ana Paula").And.NotContain("Vila Verde");
    }

    [Fact]
    public async Task NotifyAsync_DoesNotHideFailuresThatAreNotDeliveryErrors()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));
        RegistrationRequestNotifier notifier = Create("dono@example.com", "https://urban-pro.io");

        Func<Task> notify = () => notifier.NotifyAsync(Request, default);

        await notify.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().BeEmpty();
    }

    private void Capture()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _captured = m);
    }

    private RegistrationRequestNotifier Create(string ownerAddress, string baseUrl) =>
        new(_sender.Object, new WebLinksOptions { WebBaseUrl = baseUrl }, new SuperAdminOptions { Email = ownerAddress }, _logger);
}
