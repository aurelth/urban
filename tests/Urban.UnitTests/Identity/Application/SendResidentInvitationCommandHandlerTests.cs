using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Invitations.SendResidentInvitation;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

public class SendResidentInvitationCommandHandlerTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly Guid CondominiumId = Guid.CreateVersion7();

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailVerifications> _verifications = new();
    private readonly Mock<ISecretGenerator> _secrets = new();
    private readonly Mock<IResidentInvitationNotifier> _notifier = new();
    private readonly Mock<ICondominiumDirectory> _directory = new();
    private readonly Mock<IClock> _clock = new();
    private readonly List<string> _calls = [];
    private readonly SendResidentInvitationCommandHandler _handler;

    public SendResidentInvitationCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _secrets.Setup(s => s.Generate()).Returns(new GeneratedSecret("invite-secret", "invite-hash"));
        _directory
            .Setup(d => d.GetNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [CondominiumId] = "Residencial Vila Verde" });
        _verifications
            .Setup(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("stored"));
        _notifier
            .Setup(n => n.SendAsync(It.IsAny<Email>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("sent"));
        _handler = new SendResidentInvitationCommandHandler(_accounts.Object, _verifications.Object, _secrets.Object, _notifier.Object, _directory.Object, _clock.Object);
    }

    [Fact]
    public async Task Handle_ForAPendingResident_StoresAnInvitationLinkOf72HoursAndSendsTheSecretWithTheCondominiumName()
    {
        User resident = PendingResident();
        EmailVerificationToken? added = null;
        _verifications
            .Setup(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailVerificationToken, CancellationToken>((t, _) => added = t);

        Result result = await _handler.Handle(new SendResidentInvitationCommand(resident.Id), default);

        result.IsSuccess.Should().BeTrue();
        added!.UserId.Should().Be(resident.Id);
        added.TokenHash.Should().Be("invite-hash");
        added.ExpiresAt.Should().Be(Now.AddHours(72));
        _notifier.Verify(
            n => n.SendAsync(It.Is<Email>(e => e.Value == "maria@exemplo.com"), "Residencial Vila Verde", "invite-secret", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StoresTheLinkBeforeSendingIt()
    {
        User resident = PendingResident();

        await _handler.Handle(new SendResidentInvitationCommand(resident.Id), default);

        _calls.Should().Equal("stored", "sent");
    }

    [Fact]
    public async Task Handle_WhenTheCondominiumNameIsUnknown_StillSendsTheInvitationWithoutIt()
    {
        User resident = PendingResident();
        _directory
            .Setup(d => d.GetNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        await _handler.Handle(new SendResidentInvitationCommand(resident.Id), default);

        _notifier.Verify(n => n.SendAsync(It.IsAny<Email>(), null, "invite-secret", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenALinkWasIssuedAMomentAgo_SendsNothing()
    {
        User resident = PendingResident();
        _verifications
            .Setup(v => v.FindLatestOfUserAsync(resident.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailVerificationToken.IssueInvitation(resident.Id, "recent", Now.AddSeconds(-30)));

        Result result = await _handler.Handle(new SendResidentInvitationCommand(resident.Id), default);

        result.IsSuccess.Should().BeTrue();
        NothingWasSent();
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    public async Task Handle_ForAnAccountThatIsNoLongerPending_SendsNothing(AccountStatus status)
    {
        User resident = UserBuilder.InStatus(status, UserRole.Resident, "maria@exemplo.com", CondominiumId);
        _accounts.Setup(a => a.FindByIdAsync(resident.Id, It.IsAny<CancellationToken>())).ReturnsAsync(resident);

        Result result = await _handler.Handle(new SendResidentInvitationCommand(resident.Id), default);

        result.IsSuccess.Should().BeTrue();
        NothingWasSent();
    }

    [Fact]
    public async Task Handle_ForAnUnknownUser_SendsNothing()
    {
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _handler.Handle(new SendResidentInvitationCommand(userId), default);

        result.IsSuccess.Should().BeTrue();
        NothingWasSent();
    }

    [Fact]
    public async Task Handle_ForAPendingAccountThatIsNotAResident_SendsNothing()
    {
        User manager = UserBuilder.InStatus(AccountStatus.PendingVerification, UserRole.Manager, "sindico@example.com", CondominiumId);
        _accounts.Setup(a => a.FindByIdAsync(manager.Id, It.IsAny<CancellationToken>())).ReturnsAsync(manager);

        Result result = await _handler.Handle(new SendResidentInvitationCommand(manager.Id), default);

        result.IsSuccess.Should().BeTrue();
        NothingWasSent();
    }

    private User PendingResident()
    {
        User resident = UserBuilder.InStatus(AccountStatus.PendingVerification, UserRole.Resident, "maria@exemplo.com", CondominiumId);
        _accounts.Setup(a => a.FindByIdAsync(resident.Id, It.IsAny<CancellationToken>())).ReturnsAsync(resident);

        return resident;
    }

    private void NothingWasSent()
    {
        _verifications.Verify(v => v.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.SendAsync(It.IsAny<Email>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
