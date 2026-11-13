using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Contact;
using Urban.Identity.Application.Contact.ListContactMessages;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Contact;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class ListContactMessagesQueryHandlerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IContactMessages> _messages = new();
    private readonly Mock<ICondominiumDirectory> _condominiums = new();
    private readonly ListContactMessagesQueryHandler _handler;
    private readonly User _admin;

    public ListContactMessagesQueryHandlerTests()
    {
        _admin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, "dono@example.com");
        _accounts.Setup(a => a.FindByIdAsync(_admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_admin);
        _condominiums
            .Setup(c => c.GetNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _handler = new ListContactMessagesQueryHandler(_accounts.Object, _messages.Object, _condominiums.Object);
    }

    [Fact]
    public async Task Handle_ShowsEachMessageWithTheSenderAndTheRealNameOfTheCondominium()
    {
        User ana = UserBuilder.EnrolledManager("Ana", "ana@example.com");
        ContactMessage unread = ContactMessageBuilder.Unread(ana.Id, ana.CondominiumId, "Corrigir o CNPJ", "O CNPJ está errado.");
        ContactMessage read = ContactMessageBuilder.Read(ana.Id, UserBuilder.Now.AddHours(2), ana.CondominiumId, "Dúvida", "Como funciona o rateio?");
        _messages
            .Setup(m => m.ListAsync(false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactMessagePage([new(unread, ana), new(read, ana)], 2));
        _condominiums
            .Setup(c => c.GetNamesAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == ana.CondominiumId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [ana.CondominiumId!.Value] = "Residencial Vila Verde" });

        Result<ContactMessageListing> result = await _handler.Handle(new ListContactMessagesQuery(_admin.Id), Ct);

        result.IsSuccess.Should().BeTrue();
        result.Value.Total.Should().Be(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        result.Value.Items.Should().Equal(
            new ContactMessageSummary(unread.Id, ana.Id, "ana@example.com", "Ana", "Residencial Vila Verde", "Corrigir o CNPJ", "O CNPJ está errado.", UserBuilder.Now, null),
            new ContactMessageSummary(read.Id, ana.Id, "ana@example.com", "Ana", "Residencial Vila Verde", "Dúvida", "Como funciona o rateio?", UserBuilder.Now, UserBuilder.Now.AddHours(2)));
    }

    [Fact]
    public async Task Handle_ShowsTheRepliesOfEachMessageWithTheirFiles()
    {
        User ana = UserBuilder.EnrolledManager("Ana", "ana@example.com");
        ContactMessage answered = ContactMessageBuilder.Read(ana.Id, UserBuilder.Now.AddHours(2), ana.CondominiumId, "Corrigir o CNPJ", "O CNPJ está errado.");
        ContactMessage waiting = ContactMessageBuilder.Unread(ana.Id, ana.CondominiumId, "Dúvida", "Como funciona o rateio?");
        ContactReply first = ContactReplyBuilder.Text(answered.Id, _admin.Id, "Já corrigimos.", UserBuilder.Now.AddHours(2));
        ContactReply second = ContactReplyBuilder.WithFiles(
            answered.Id,
            _admin.Id,
            [new("ata.pdf", SampleFiles.Pdf())],
            "Segue o comprovante.",
            UserBuilder.Now.AddHours(3));
        _messages
            .Setup(m => m.ListAsync(false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactMessagePage([new(answered, ana, [first, second]), new(waiting, ana)], 2));

        Result<ContactMessageListing> result = await _handler.Handle(new ListContactMessagesQuery(_admin.Id), Ct);

        ContactMessageSummary[] items = [.. result.Value.Items];
        items[0].Replies.Should().HaveCount(2);
        items[0].Replies[0].Should().BeEquivalentTo(new ContactReplySummary(first.Id, first.SentAt, "Já corrigimos.", []));
        items[0].Replies[1].Id.Should().Be(second.Id);
        items[0].Replies[1].Body.Should().Be("Segue o comprovante.");
        items[0].Replies[1].Attachments.Should().Equal(new ContactReplyAttachment("ata.pdf", "application/pdf", SampleFiles.Pdf().Length));
        items[1].Replies.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AsksTheDirectoryOnceForTheWholePageWithoutRepeatedIds()
    {
        User ana = UserBuilder.EnrolledManager("Ana", "ana@example.com");
        User bia = UserBuilder.ManagerWithoutCondominium(address: "bia@example.com");
        _messages
            .Setup(m => m.ListAsync(false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactMessagePage(
                [
                    new(ContactMessageBuilder.Unread(ana.Id, ana.CondominiumId), ana),
                    new(ContactMessageBuilder.Unread(ana.Id, ana.CondominiumId), ana),
                    new(ContactMessageBuilder.Unread(bia.Id), bia),
                ],
                3));

        await _handler.Handle(new ListContactMessagesQuery(_admin.Id), Ct);

        _condominiums.Verify(c => c.GetNamesAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Single() == ana.CondominiumId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ForASenderWhoHasNotRegisteredTheCondominium_UsesTheNameTypedInTheRequestAndSkipsTheDirectory()
    {
        User requested = UserBuilder.Requested(address: "bia@example.com", name: "Bia", condominiumName: "Edifício Sol");
        ContactMessage message = ContactMessageBuilder.Unread(requested.Id);
        _messages
            .Setup(m => m.ListAsync(false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactMessagePage([new(message, requested)], 1));

        Result<ContactMessageListing> result = await _handler.Handle(new ListContactMessagesQuery(_admin.Id), Ct);

        result.Value.Items.Single().CondominiumName.Should().Be("Edifício Sol");
        _condominiums.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, 2, 50)]
    [InlineData(false, 3, 10)]
    public async Task Handle_PassesTheFilterAndThePageToTheStore(bool unreadOnly, int page, int pageSize)
    {
        _messages
            .Setup(m => m.ListAsync(unreadOnly, page, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactMessagePage([], 0));

        Result<ContactMessageListing> result = await _handler.Handle(new ListContactMessagesQuery(_admin.Id, unreadOnly, page, pageSize), Ct);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Page.Should().Be(page);
        result.Value.PageSize.Should().Be(pageSize);
        _messages.Verify(m => m.ListAsync(unreadOnly, page, pageSize, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Handle_WithAnInvalidPage_FailsWithoutReadingAnything(int page, int pageSize)
    {
        Result<ContactMessageListing> result = await _handler.Handle(new ListContactMessagesQuery(_admin.Id, false, page, pageSize), Ct);

        result.Error.Should().Be(ContactErrors.PageInvalid);
        _messages.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(UserRole.Manager, AccountStatus.Active)]
    [InlineData(UserRole.Resident, AccountStatus.Active)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Suspended)]
    [InlineData(UserRole.SuperAdmin, AccountStatus.Deactivated)]
    public async Task Handle_ForAnyoneWhoIsNotAnActiveSuperAdmin_IsForbiddenAndReadsNothing(UserRole role, AccountStatus status)
    {
        User actor = UserBuilder.InStatus(status, role, $"{role}-{status}@example.com");
        _accounts.Setup(a => a.FindByIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);

        Result<ContactMessageListing> result = await _handler.Handle(new ListContactMessagesQuery(actor.Id), Ct);

        result.Error.Should().Be(ContactErrors.ReadForbidden);
        _messages.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAnUnknownActor_IsForbidden()
    {
        Result<ContactMessageListing> result = await _handler.Handle(new ListContactMessagesQuery(Guid.CreateVersion7()), Ct);

        result.Error.Should().Be(ContactErrors.ReadForbidden);
        _messages.VerifyNoOtherCalls();
    }
}
