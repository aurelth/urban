using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Contact;
using Urban.Identity.Application.Contact.SendContactReply;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class SendContactReplyCommandHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Later = UserBuilder.Now.AddHours(4);

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IContactMessages> _messages = new();
    private readonly Mock<IContactReplies> _replies = new();
    private readonly Mock<IContactReplyMailer> _mailer = new();
    private readonly Mock<IClock> _clock = new();
    private readonly SendContactReplyCommandHandler _handler;
    private readonly User _admin;
    private readonly User _ana;
    private readonly ContactMessage _message;

    public SendContactReplyCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Later);
        _admin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com");
        _ana = UserBuilder.EnrolledManager("Ana", "ana@example.com");
        _message = ContactMessageBuilder.Unread(_ana.Id, _ana.CondominiumId, "Corrigir o CNPJ", "O CNPJ do meu condomínio está errado.");
        _accounts.Setup(a => a.FindByIdAsync(_admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_admin);
        _accounts.Setup(a => a.FindByIdAsync(_ana.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_ana);
        _messages.Setup(m => m.FindAsync(_message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_message);
        _mailer.Setup(m => m.SendAsync(It.IsAny<ContactReplyEmail>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _handler = new SendContactReplyCommandHandler(_accounts.Object, _messages.Object, _replies.Object, _mailer.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_SendsTheEmailToTheSenderWithTheAdminAsReplyTo()
    {
        byte[] pdf = SampleFiles.Pdf();

        Result<ContactReplyReceipt> result = await _handler.Handle(
            new SendContactReplyCommand(_admin.Id, _message.Id, "  Já corrigimos o CNPJ.  ", [new("ata.pdf", pdf)]),
            Ct);

        result.IsSuccess.Should().BeTrue();
        _mailer.Verify(
            m => m.SendAsync(
                It.Is<ContactReplyEmail>(e =>
                    e.To == "ana@example.com"
                    && e.RecipientName == "Ana"
                    && e.ReplyTo == "dono@example.com"
                    && e.OriginalSubject == "Corrigir o CNPJ"
                    && e.OriginalSentAt == _message.CreatedAt
                    && e.Body == "Já corrigimos o CNPJ."
                    && e.Files.Count == 1
                    && e.Files[0].FileName == "ata.pdf"
                    && e.Files[0].Content == pdf),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_RecordsTheReplyAfterTheEmailWentOutAndReturnsTheReceipt()
    {
        byte[] pdf = SampleFiles.Pdf();
        ContactReply? stored = null;
        _replies
            .Setup(r => r.AddAsync(It.IsAny<ContactReply>(), It.IsAny<CancellationToken>()))
            .Callback<ContactReply, CancellationToken>((reply, _) => stored = reply)
            .Returns(Task.CompletedTask);

        Result<ContactReplyReceipt> result = await _handler.Handle(
            new SendContactReplyCommand(_admin.Id, _message.Id, "Já corrigimos o CNPJ.", [new("ata.pdf", pdf)]),
            Ct);

        stored.Should().NotBeNull();
        stored!.MessageId.Should().Be(_message.Id);
        stored.AuthorId.Should().Be(_admin.Id);
        stored.Body.Should().Be("Já corrigimos o CNPJ.");
        stored.SentAt.Should().Be(Later);
        stored.Attachments.Should().Equal(new ContactReplyAttachment("ata.pdf", "application/pdf", pdf.Length));
        result.Value.Id.Should().Be(stored.Id);
        result.Value.SentAt.Should().Be(Later);
        result.Value.Attachments.Should().Equal(stored.Attachments);
    }

    [Fact]
    public async Task Handle_SendsTheEmailBeforeRecordingAnything()
    {
        List<string> calls = [];
        _mailer
            .Setup(m => m.SendAsync(It.IsAny<ContactReplyEmail>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("email"))
            .ReturnsAsync(Result.Success());
        _replies
            .Setup(r => r.AddAsync(It.IsAny<ContactReply>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("gravação"))
            .Returns(Task.CompletedTask);

        await _handler.Handle(new SendContactReplyCommand(_admin.Id, _message.Id, "Já corrigimos o CNPJ.", []), Ct);

        calls.Should().Equal("email", "gravação");
    }

    [Fact]
    public async Task Handle_MarksAnUnreadMessageAsRead()
    {
        await _handler.Handle(new SendContactReplyCommand(_admin.Id, _message.Id, "Já corrigimos o CNPJ.", []), Ct);

        _message.ReadAt.Should().Be(Later);
        _messages.Verify(m => m.MarkReadAsync(_message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_KeepsTheFirstReadingOfAMessageThatWasAlreadyRead()
    {
        DateTimeOffset firstReading = UserBuilder.Now.AddHours(1);
        ContactMessage read = ContactMessageBuilder.Read(_ana.Id, firstReading, _ana.CondominiumId);
        _messages.Setup(m => m.FindAsync(read.Id, It.IsAny<CancellationToken>())).ReturnsAsync(read);

        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(_admin.Id, read.Id, "Já corrigimos o CNPJ.", []), Ct);

        result.IsSuccess.Should().BeTrue();
        read.ReadAt.Should().Be(firstReading);
        _messages.Verify(m => m.MarkReadAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _replies.Verify(r => r.AddAsync(It.IsAny<ContactReply>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheEmailIsNotDelivered_RecordsNothingAndKeepsTheMessageUnread()
    {
        _mailer
            .Setup(m => m.SendAsync(It.IsAny<ContactReplyEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ContactErrors.ReplyNotDelivered));

        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(_admin.Id, _message.Id, "Já corrigimos o CNPJ.", []), Ct);

        result.Error.Should().Be(ContactErrors.ReplyNotDelivered);
        _replies.Verify(r => r.AddAsync(It.IsAny<ContactReply>(), It.IsAny<CancellationToken>()), Times.Never);
        _messages.Verify(m => m.MarkReadAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _message.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAnInvalidText_SendsNothingAndRecordsNothing()
    {
        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(_admin.Id, _message.Id, "ok", []), Ct);

        result.Error.Should().Be(ContactReplyErrors.BodyInvalid);
        VerifyNothingWasSentOrRecorded();
    }

    [Fact]
    public async Task Handle_WithAnInvalidFile_SendsNothingAndRecordsNothing()
    {
        Result<ContactReplyReceipt> result = await _handler.Handle(
            new SendContactReplyCommand(_admin.Id, _message.Id, "Já corrigimos o CNPJ.", [new("programa.exe", SampleFiles.Pdf())]),
            Ct);

        result.Error.Should().Be(ContactReplyErrors.AttachmentTypeNotAllowed);
        VerifyNothingWasSentOrRecorded();
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveSuperAdmin_IsForbiddenAndDoesNothing(UserRole role, AccountStatus status)
    {
        User actor = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(actor.Id, _message.Id, "Já corrigimos o CNPJ.", []), Ct);

        result.Error.Should().Be(ContactErrors.ReplyForbidden);
        _messages.Verify(m => m.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingWasSentOrRecorded();
    }

    [Fact]
    public async Task Handle_ForAnUnknownActor_IsForbidden()
    {
        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(Guid.CreateVersion7(), _message.Id, "Já corrigimos o CNPJ.", []), Ct);

        result.Error.Should().Be(ContactErrors.ReplyForbidden);
        VerifyNothingWasSentOrRecorded();
    }

    [Fact]
    public async Task Handle_ForAnUnknownMessage_ReturnsNotFound()
    {
        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(_admin.Id, Guid.CreateVersion7(), "Já corrigimos o CNPJ.", []), Ct);

        result.Error.Should().Be(ContactErrors.MessageNotFound);
        VerifyNothingWasSentOrRecorded();
    }

    [Fact]
    public async Task Handle_WhenTheSenderNoLongerExists_ReturnsRecipientUnavailable()
    {
        _accounts.Setup(a => a.FindByIdAsync(_ana.Id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(_admin.Id, _message.Id, "Já corrigimos o CNPJ.", []), Ct);

        result.Error.Should().Be(ContactErrors.ReplyRecipientUnavailable);
        VerifyNothingWasSentOrRecorded();
    }

    [Fact]
    public async Task Handle_WhenTheSenderAccountWasDeleted_ReturnsRecipientUnavailable()
    {
        User deleted = UserBuilder.InStatus(AccountStatus.Deleted, UserRole.Manager, "apagado@example.com");
        ContactMessage message = ContactMessageBuilder.Unread(deleted.Id);
        _accounts.Setup(a => a.FindByIdAsync(deleted.Id, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);

        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(_admin.Id, message.Id, "Já corrigimos o CNPJ.", []), Ct);

        result.Error.Should().Be(ContactErrors.ReplyRecipientUnavailable);
        VerifyNothingWasSentOrRecorded();
    }

    [Fact]
    public async Task Handle_StillAnswersASenderWhoWasSuspendedAfterWriting()
    {
        User suspended = UserBuilder.InStatus(AccountStatus.Suspended, UserRole.Manager, "suspensa@example.com");
        ContactMessage message = ContactMessageBuilder.Unread(suspended.Id);
        _accounts.Setup(a => a.FindByIdAsync(suspended.Id, It.IsAny<CancellationToken>())).ReturnsAsync(suspended);
        _messages.Setup(m => m.FindAsync(message.Id, It.IsAny<CancellationToken>())).ReturnsAsync(message);

        Result<ContactReplyReceipt> result = await _handler.Handle(new SendContactReplyCommand(_admin.Id, message.Id, "Sua conta foi suspensa por falta de dados.", []), Ct);

        result.IsSuccess.Should().BeTrue();
        _mailer.Verify(m => m.SendAsync(It.Is<ContactReplyEmail>(e => e.To == "suspensa@example.com"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ToString_NeverShowsTheTextOrTheContentOfTheFiles()
    {
        SendContactReplyCommand command = new(_admin.Id, _message.Id, "Texto reservado da resposta.", [new("segredo.pdf", SampleFiles.Pdf())]);

        command.ToString().Should().NotContain("Texto reservado").And.NotContain("segredo.pdf");
    }

    private void VerifyNothingWasSentOrRecorded()
    {
        _mailer.Verify(m => m.SendAsync(It.IsAny<ContactReplyEmail>(), It.IsAny<CancellationToken>()), Times.Never);
        _replies.Verify(r => r.AddAsync(It.IsAny<ContactReply>(), It.IsAny<CancellationToken>()), Times.Never);
        _messages.Verify(m => m.MarkReadAsync(It.IsAny<ContactMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
