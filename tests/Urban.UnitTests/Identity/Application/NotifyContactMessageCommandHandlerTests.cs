using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Contact;
using Urban.Identity.Application.Contact.NotifyContactMessage;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class NotifyContactMessageCommandHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Mock<IContactMessages> _messages = new();
    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<ICondominiumDirectory> _condominiums = new();
    private readonly Mock<IContactMessageNotifier> _notifier = new();
    private readonly NotifyContactMessageCommandHandler _handler;

    public NotifyContactMessageCommandHandlerTests()
    {
        _handler = new NotifyContactMessageCommandHandler(_messages.Object, _accounts.Object, _condominiums.Object, _notifier.Object);
    }

    [Fact]
    public async Task Handle_ForAnUnreadMessage_SendsTheMessageTheSenderAndTheCondominiumNameToTheNotifier()
    {
        User manager = UserBuilder.EnrolledManager("Ana", "ana@example.com");
        ContactMessage message = ContactMessageBuilder.Unread(manager.Id, manager.CondominiumId);
        Setup(message, manager, "Residencial Vila Verde");
        ContactMessageSummary? sent = null;
        _notifier
            .Setup(n => n.NotifyAsync(It.IsAny<ContactMessageSummary>(), It.IsAny<CancellationToken>()))
            .Callback<ContactMessageSummary, CancellationToken>((s, _) => sent = s)
            .Returns(Task.CompletedTask);

        Result result = await _handler.Handle(new NotifyContactMessageCommand(message.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        sent.Should().Be(new ContactMessageSummary(
            message.Id,
            manager.Id,
            "ana@example.com",
            "Ana",
            "Residencial Vila Verde",
            "Corrigir o CNPJ",
            "O CNPJ do meu condomínio está errado.",
            UserBuilder.Now,
            null));
    }

    [Fact]
    public async Task Handle_ForAManagerWithoutACondominiumYet_SendsWithoutAskingTheDirectory()
    {
        User manager = UserBuilder.ManagerWithoutCondominium();
        ContactMessage message = ContactMessageBuilder.Unread(manager.Id);
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);
        _accounts.Setup(a => a.FindByIdAsync(manager.Id, It.IsAny<CancellationToken>())).ReturnsAsync(manager);
        ContactMessageSummary? sent = null;
        _notifier
            .Setup(n => n.NotifyAsync(It.IsAny<ContactMessageSummary>(), It.IsAny<CancellationToken>()))
            .Callback<ContactMessageSummary, CancellationToken>((s, _) => sent = s)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new NotifyContactMessageCommand(message.Id), Ct);

        sent!.CondominiumName.Should().BeNull();
        sent.SenderName.Should().BeNull();
        _condominiums.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownMessage_SucceedsWithoutNotifying()
    {
        Result result = await _handler.Handle(new NotifyContactMessageCommand(Guid.CreateVersion7()), Ct);

        result.IsSuccess.Should().BeTrue();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAMessageThatWasAlreadyRead_SucceedsWithoutNotifying()
    {
        User manager = UserBuilder.EnrolledManager("Ana");
        ContactMessage message = ContactMessageBuilder.Read(manager.Id, UserBuilder.Now.AddHours(1));
        Setup(message, manager, "Residencial Vila Verde");

        Result result = await _handler.Handle(new NotifyContactMessageCommand(message.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheSenderNoLongerExists_SucceedsWithoutNotifying()
    {
        ContactMessage message = ContactMessageBuilder.Unread(Guid.CreateVersion7());
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);

        Result result = await _handler.Handle(new NotifyContactMessageCommand(message.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheNotifierFails_LetsTheFailureReachTheRetryPolicy()
    {
        User manager = UserBuilder.EnrolledManager("Ana");
        ContactMessage message = ContactMessageBuilder.Unread(manager.Id, manager.CondominiumId);
        Setup(message, manager, "Residencial Vila Verde");
        _notifier
            .Setup(n => n.NotifyAsync(It.IsAny<ContactMessageSummary>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falhou"));

        Func<Task> handle = () => _handler.Handle(new NotifyContactMessageCommand(message.Id), Ct);

        await handle.Should().ThrowAsync<InvalidOperationException>();
    }

    private void Setup(ContactMessage message, User sender, string condominiumName)
    {
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);
        _accounts.Setup(a => a.FindByIdAsync(sender.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sender);
        _condominiums
            .Setup(c => c.GetNamesAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == sender.CondominiumId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [sender.CondominiumId!.Value] = condominiumName });
    }
}
