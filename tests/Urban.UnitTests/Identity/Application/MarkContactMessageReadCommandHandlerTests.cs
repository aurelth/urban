using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Contact;
using Urban.Identity.Application.Contact.MarkContactMessageRead;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class MarkContactMessageReadCommandHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Later = UserBuilder.Now.AddHours(4);

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IContactMessages> _messages = new();
    private readonly Mock<IClock> _clock = new();
    private readonly MarkContactMessageReadCommandHandler _handler;
    private readonly User _admin;

    public MarkContactMessageReadCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Later);
        _admin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com");
        _accounts.Setup(a => a.FindByIdAsync(_admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_admin);
        _handler = new MarkContactMessageReadCommandHandler(_accounts.Object, _messages.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_OnAnUnreadMessage_MarksItReadAndSavesIt()
    {
        ContactMessage message = ContactMessageBuilder.Unread(Guid.CreateVersion7());
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);

        Result result = await _handler.Handle(new MarkContactMessageReadCommand(_admin.Id, message.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        message.ReadAt.Should().Be(Later);
        _messages.Verify(m => m.MarkReadAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OnAMessageThatWasAlreadyRead_SucceedsAndWritesNothing()
    {
        DateTimeOffset firstReading = UserBuilder.Now.AddHours(1);
        ContactMessage message = ContactMessageBuilder.Read(Guid.CreateVersion7(), firstReading);
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);

        Result result = await _handler.Handle(new MarkContactMessageReadCommand(_admin.Id, message.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        message.ReadAt.Should().Be(firstReading);
        _messages.Verify(m => m.MarkReadAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ForAnUnknownMessage_ReturnsNotFound()
    {
        Result result = await _handler.Handle(new MarkContactMessageReadCommand(_admin.Id, Guid.CreateVersion7()), Ct);

        result.Error.Should().Be(ContactErrors.MessageNotFound);
        _messages.Verify(m => m.MarkReadAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveSuperAdmin_IsForbiddenAndChangesNothing(UserRole role, AccountStatus status)
    {
        User actor = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        ContactMessage message = ContactMessageBuilder.Unread(Guid.CreateVersion7());
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);

        Result result = await _handler.Handle(new MarkContactMessageReadCommand(actor.Id, message.Id), Ct);

        result.Error.Should().Be(ContactErrors.ReadForbidden);
        message.IsRead.Should().BeFalse();
        _messages.Verify(m => m.MarkReadAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
