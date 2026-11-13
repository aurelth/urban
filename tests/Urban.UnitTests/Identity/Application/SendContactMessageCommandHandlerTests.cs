using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Contact;
using Urban.Identity.Application.Contact.SendContactMessage;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class SendContactMessageCommandHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Later = UserBuilder.Now.AddHours(3);

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IContactMessages> _messages = new();
    private readonly Mock<IContactMessageDispatcher> _dispatcher = new();
    private readonly Mock<IClock> _clock = new();
    private readonly SendContactMessageCommandHandler _handler;

    public SendContactMessageCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Later);
        _handler = new SendContactMessageCommandHandler(_accounts.Object, _messages.Object, _dispatcher.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_ForAnActiveManager_StoresTheMessageWithTheSenderAndTheirCondominium()
    {
        User manager = Sender(UserBuilder.EnrolledManager("Ana", "ana@example.com"));
        ContactMessage? stored = null;
        _messages
            .Setup(m => m.AddAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ContactMessage, CancellationToken>((message, _) => stored = message)
            .Returns(Task.CompletedTask);

        Result<ContactMessageReceipt> result = await _handler.Handle(new SendContactMessageCommand(manager.Id, "  Corrigir o CNPJ ", " O CNPJ está errado, pode corrigir? "), Ct);

        result.IsSuccess.Should().BeTrue();
        stored.Should().NotBeNull();
        stored!.SenderId.Should().Be(manager.Id);
        stored.CondominiumId.Should().Be(manager.CondominiumId);
        stored.Subject.Should().Be("Corrigir o CNPJ");
        stored.Body.Should().Be("O CNPJ está errado, pode corrigir?");
        stored.CreatedAt.Should().Be(Later);
        result.Value.Should().Be(new ContactMessageReceipt(stored.Id, Later));
    }

    [Fact]
    public async Task Handle_ForAManagerWhoHasNotRegisteredTheCondominiumYet_StoresTheMessageWithoutACondominium()
    {
        User manager = Sender(UserBuilder.ManagerWithoutCondominium());
        ContactMessage? stored = null;
        _messages
            .Setup(m => m.AddAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ContactMessage, CancellationToken>((message, _) => stored = message)
            .Returns(Task.CompletedTask);

        Result<ContactMessageReceipt> result = await _handler.Handle(new SendContactMessageCommand(manager.Id, "Dúvida", "Como cadastro o condomínio?"), Ct);

        result.IsSuccess.Should().BeTrue();
        stored!.CondominiumId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_QueuesTheNotificationOnlyAfterTheMessageIsStored()
    {
        User manager = Sender(UserBuilder.EnrolledManager("Ana"));
        List<string> order = [];
        Guid storedId = Guid.Empty;
        _messages
            .Setup(m => m.AddAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ContactMessage, CancellationToken>((message, _) =>
            {
                storedId = message.Id;
                order.Add("stored");
            })
            .Returns(Task.CompletedTask);
        _dispatcher
            .Setup(d => d.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) =>
            {
                id.Should().Be(storedId);
                order.Add("queued");
            })
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SendContactMessageCommand(manager.Id, "Assunto", "Texto da mensagem."), Ct);

        order.Should().Equal("stored", "queued");
    }

    [Fact]
    public async Task Handle_WithAnInvalidSubject_FailsAndStoresNothing()
    {
        User manager = Sender(UserBuilder.EnrolledManager("Ana"));

        Result<ContactMessageReceipt> result = await _handler.Handle(new SendContactMessageCommand(manager.Id, "ab", "Texto da mensagem."), Ct);

        result.Error.Should().Be(ContactMessageErrors.SubjectInvalid);
        NothingWasStoredOrQueued();
    }

    [Fact]
    public async Task Handle_WithAnInvalidBody_FailsAndStoresNothing()
    {
        User manager = Sender(UserBuilder.EnrolledManager("Ana"));

        Result<ContactMessageReceipt> result = await _handler.Handle(new SendContactMessageCommand(manager.Id, "Assunto", "curta"), Ct);

        result.Error.Should().Be(ContactMessageErrors.BodyInvalid);
        NothingWasStoredOrQueued();
    }

    [Theory]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Active)]
    [InlineData(UserRole.Manager, AccountStatus.Suspended)]
    [InlineData(UserRole.Manager, AccountStatus.Deactivated)]
    [InlineData(UserRole.Manager, AccountStatus.PendingVerification)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveManager_IsForbiddenAndStoresNothing(UserRole role, AccountStatus status)
    {
        User sender = Sender(UserBuilder.InStatus(status, role, $"{role}-{status}@example.com"));

        Result<ContactMessageReceipt> result = await _handler.Handle(new SendContactMessageCommand(sender.Id, "Assunto", "Texto da mensagem."), Ct);

        result.Error.Should().Be(ContactErrors.SendForbidden);
        NothingWasStoredOrQueued();
    }

    [Fact]
    public async Task Handle_ForAnUnknownAccount_IsForbidden()
    {
        Result<ContactMessageReceipt> result = await _handler.Handle(new SendContactMessageCommand(Guid.CreateVersion7(), "Assunto", "Texto da mensagem."), Ct);

        result.Error.Should().Be(ContactErrors.SendForbidden);
        NothingWasStoredOrQueued();
    }

    [Fact]
    public async Task Handle_WhenStoringFails_DoesNotQueueTheNotification()
    {
        User manager = Sender(UserBuilder.EnrolledManager("Ana"));
        _messages
            .Setup(m => m.AddAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("banco fora do ar"));

        Func<Task> handle = () => _handler.Handle(new SendContactMessageCommand(manager.Id, "Assunto", "Texto da mensagem."), Ct);

        await handle.Should().ThrowAsync<InvalidOperationException>();
        _dispatcher.VerifyNoOtherCalls();
    }

    private User Sender(User user)
    {
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    private void NothingWasStoredOrQueued()
    {
        _messages.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }
}
