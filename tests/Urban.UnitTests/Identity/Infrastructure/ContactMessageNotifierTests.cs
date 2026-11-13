using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Urban.Identity.Application.Contact;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.Identity.Infrastructure;

public class ContactMessageNotifierTests
{
    private static readonly ContactMessageSummary Message = new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        "ana@example.com",
        "Ana Paula",
        "Residencial Vila Verde",
        "Corrigir o CNPJ",
        "O CNPJ do meu condomínio está errado.\nPode corrigir?",
        new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero),
        null);

    private readonly Mock<IEmailSender> _sender = new();
    private readonly ListLogger<ContactMessageNotifier> _logger = new();

    private EmailMessage? _captured;

    [Fact]
    public async Task NotifyAsync_SendsAPortugueseMessageToTheOwnerWithTheSenderTheTextAndTheLinkToTheInbox()
    {
        Capture();
        ContactMessageNotifier notifier = Create("dono@example.com", "https://urban-pro.io");

        await notifier.NotifyAsync(Message, default);

        EmailMessage sent = _captured!;
        sent.To.Should().Be("dono@example.com");
        sent.Subject.Should().Be("Nova mensagem de contato no Urban");
        sent.TextBody.Should().Contain("Ana Paula").And.Contain("ana@example.com").And.Contain("Residencial Vila Verde");
        sent.TextBody.Should().Contain("Corrigir o CNPJ").And.Contain("O CNPJ do meu condomínio está errado.\nPode corrigir?");
        sent.TextBody.Should().Contain("https://urban-pro.io/admin/mensagens");
    }

    [Fact]
    public async Task NotifyAsync_WithoutASenderNameOrCondominium_UsesTheEmailAndSaysTheCondominiumIsNotRegisteredYet()
    {
        Capture();
        ContactMessageNotifier notifier = Create("dono@example.com", "https://urban-pro.io");

        await notifier.NotifyAsync(Message with { SenderName = null, CondominiumName = null }, default);

        _captured!.TextBody.Should().Contain("Nome: ana@example.com");
        _captured.TextBody.Should().Contain("Condomínio: ainda não cadastrado");
    }

    [Fact]
    public async Task NotifyAsync_KeepsTheSitePathWhenTheBaseAddressHasOne()
    {
        Capture();
        ContactMessageNotifier notifier = Create("dono@example.com", "https://exemplo.com/app/");

        await notifier.NotifyAsync(Message, default);

        _captured!.TextBody.Should().Contain("https://exemplo.com/app/admin/mensagens");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NotifyAsync_WithoutAConfiguredOwnerAddress_SendsNothingAndLogsAWarningWithoutPersonalData(string address)
    {
        ContactMessageNotifier notifier = Create(address, "https://urban-pro.io");

        await notifier.NotifyAsync(Message, default);

        _sender.VerifyNoOtherCalls();
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().NotContain("ana@example.com").And.NotContain("Ana Paula").And.NotContain("CNPJ");
    }

    [Fact]
    public async Task NotifyAsync_WhenTheProviderFails_LogsAnErrorWithoutPersonalDataAndRethrowsForTheRetry()
    {
        EmailDeliveryException failure = new("O Brevo recusou o envio (HTTP 401, código unauthorized).");
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        ContactMessageNotifier notifier = Create("dono@example.com", "https://urban-pro.io");

        Func<Task> notify = () => notifier.NotifyAsync(Message, default);

        (await notify.Should().ThrowAsync<EmailDeliveryException>()).Which.Should().BeSameAs(failure);
        LogEntry entry = _logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().NotContain("ana@example.com").And.NotContain("Ana Paula").And.NotContain("CNPJ");
    }

    [Fact]
    public async Task NotifyAsync_DoesNotHideFailuresThatAreNotDeliveryErrors()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bug"));
        ContactMessageNotifier notifier = Create("dono@example.com", "https://urban-pro.io");

        Func<Task> notify = () => notifier.NotifyAsync(Message, default);

        await notify.Should().ThrowAsync<InvalidOperationException>();
        _logger.Entries.Should().BeEmpty();
    }

    private void Capture()
    {
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _captured = m);
    }

    private ContactMessageNotifier Create(string ownerAddress, string baseUrl) =>
        new(_sender.Object, new WebLinksOptions { WebBaseUrl = baseUrl }, new SuperAdminOptions { Email = ownerAddress }, _logger);
}
