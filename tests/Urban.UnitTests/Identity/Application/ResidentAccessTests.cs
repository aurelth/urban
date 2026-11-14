using FluentAssertions;
using Moq;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Invitations;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Application;

/// <summary>O contrato que o módulo Condomínio usa para dar acesso a um morador: criar a conta pendente e pedir o e-mail do convite.</summary>
public class ResidentAccessTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly Guid CondominiumId = Guid.CreateVersion7();

    private readonly Mock<IUserAccounts> _accounts = new();
    private readonly Mock<IEmailVerifications> _verifications = new();
    private readonly Mock<IResidentInvitationDispatcher> _dispatcher = new();
    private readonly Mock<IClock> _clock = new();
    private readonly IResidentAccess _access;

    public ResidentAccessTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _verifications.Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _access = new ResidentAccess(_accounts.Object, _verifications.Object, _dispatcher.Object, _clock.Object);
    }

    // ---- Criar a conta ----

    [Fact]
    public async Task Create_RegistersAPendingResidentInTheCondominiumAndReturnsItsId()
    {
        User manager = GivenManager();
        User? registered = null;
        _verifications
            .Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => registered = u)
            .ReturnsAsync(true);

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "maria@exemplo.com", manager.Id, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(registered!.Id);
        registered.Role.Should().Be(UserRole.Resident);
        registered.CondominiumId.Should().Be(CondominiumId);
        registered.Status.Should().Be(AccountStatus.PendingVerification);
        registered.Email.Value.Should().Be("maria@exemplo.com");
    }

    [Fact]
    public async Task Create_DoesNotSendTheEmail_BecauseTheCallerEnqueuesItOnlyAfterTheCommit()
    {
        User manager = GivenManager();

        await _access.CreateAsync(CondominiumId, "maria@exemplo.com", manager.Id, default);

        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_WithAnInvalidEmail_FailsAndRegistersNothing()
    {
        User manager = GivenManager();

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "sem-arroba", manager.Id, default);

        result.Error.Should().Be(Email.Invalid);
        NothingWasRegistered();
    }

    [Fact]
    public async Task Create_ForAnEmailThatAlreadyHasAnAccount_FailsWithoutSayingWhoOwnsIt()
    {
        User manager = GivenManager();
        _accounts
            .Setup(a => a.FindByEmailAsync(It.Is<Email>(e => e.Value == "maria@exemplo.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserBuilder.InStatus(AccountStatus.Active));

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "maria@exemplo.com", manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.EmailUnavailable);
        NothingWasRegistered();
    }

    [Fact]
    public async Task Create_WhenAnotherRegistrationWinsTheRace_FailsAsEmailUnavailable()
    {
        User manager = GivenManager();
        _verifications.Setup(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "maria@exemplo.com", manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.EmailUnavailable);
    }

    [Fact]
    public async Task Create_ForAnUnknownActor_Fails()
    {
        Guid actorId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(actorId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "maria@exemplo.com", actorId, default);

        result.Error.Should().Be(ResidentAccessErrors.Forbidden);
        NothingWasRegistered();
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Create_ForAnActorWhoIsNotAManager_Fails(UserRole role)
    {
        User actor = UserBuilder.InStatus(AccountStatus.Active, role, condominiumId: CondominiumId);
        GivenAccount(actor);

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "maria@exemplo.com", actor.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.Forbidden);
        NothingWasRegistered();
    }

    [Fact]
    public async Task Create_ForAManagerOfAnotherCondominium_Fails()
    {
        User manager = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager, condominiumId: Guid.CreateVersion7());
        GivenAccount(manager);

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "maria@exemplo.com", manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.Forbidden);
        NothingWasRegistered();
    }

    [Fact]
    public async Task Create_ForAManagerWhoCannotSignInAnymore_FailsWithTheStateOfTheAccount()
    {
        User manager = UserBuilder.InStatus(AccountStatus.Suspended, UserRole.Manager, condominiumId: CondominiumId);
        GivenAccount(manager);

        Result<Guid> result = await _access.CreateAsync(CondominiumId, "maria@exemplo.com", manager.Id, default);

        result.Error.Should().Be(UserErrors.AccountSuspended);
        NothingWasRegistered();
    }

    // ---- Enviar o convite ----

    [Fact]
    public async Task SendInvitation_ForAPendingResident_QueuesTheEmail()
    {
        User manager = GivenManager();
        User resident = GivenResident(AccountStatus.PendingVerification);

        Result result = await _access.SendInvitationAsync(resident.Id, CondominiumId, manager.Id, default);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(d => d.EnqueueAsync(resident.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendInvitation_WhenTheLastLinkIsOlderThanTheResendInterval_QueuesAgain()
    {
        User manager = GivenManager();
        User resident = GivenResident(AccountStatus.PendingVerification);
        GivenLatestLink(resident, Now.AddMinutes(-2));

        Result result = await _access.SendInvitationAsync(resident.Id, CondominiumId, manager.Id, default);

        result.IsSuccess.Should().BeTrue();
        _dispatcher.Verify(d => d.EnqueueAsync(resident.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendInvitation_WhenALinkWentOutAMomentAgo_FailsAsTooSoonAndQueuesNothing()
    {
        User manager = GivenManager();
        User resident = GivenResident(AccountStatus.PendingVerification);
        GivenLatestLink(resident, Now.AddSeconds(-30));

        Result result = await _access.SendInvitationAsync(resident.Id, CondominiumId, manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.ResendTooSoon);
        NothingWasQueued();
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    public async Task SendInvitation_ForAResidentWhoAlreadyChoseThePassword_FailsAsNotPending(AccountStatus status)
    {
        User manager = GivenManager();
        User resident = GivenResident(status);

        Result result = await _access.SendInvitationAsync(resident.Id, CondominiumId, manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.NotPending);
        NothingWasQueued();
    }

    [Fact]
    public async Task SendInvitation_ForAnUnknownUser_FailsAsNotFound()
    {
        User manager = GivenManager();
        Guid userId = Guid.CreateVersion7();
        _accounts.Setup(a => a.FindByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Result result = await _access.SendInvitationAsync(userId, CondominiumId, manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.NotFound);
        NothingWasQueued();
    }

    [Fact]
    public async Task SendInvitation_ForAResidentOfAnotherCondominium_FailsAsNotFound()
    {
        User manager = GivenManager();
        User other = UserBuilder.InStatus(AccountStatus.PendingVerification, UserRole.Resident, "outro@example.com", Guid.CreateVersion7());
        GivenAccount(other);

        Result result = await _access.SendInvitationAsync(other.Id, CondominiumId, manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.NotFound);
        NothingWasQueued();
    }

    [Fact]
    public async Task SendInvitation_ForAnAccountThatIsNotAResident_FailsAsNotFound()
    {
        User manager = GivenManager();
        User someoneElse = UserBuilder.InStatus(AccountStatus.PendingVerification, UserRole.Manager, "sindico@example.com", CondominiumId);
        GivenAccount(someoneElse);

        Result result = await _access.SendInvitationAsync(someoneElse.Id, CondominiumId, manager.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.NotFound);
        NothingWasQueued();
    }

    [Fact]
    public async Task SendInvitation_ForAnActorWhoCannotManageTheCondominium_FailsAsForbidden()
    {
        User actor = UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, "vizinho@example.com", CondominiumId);
        GivenAccount(actor);
        User resident = GivenResident(AccountStatus.PendingVerification);

        Result result = await _access.SendInvitationAsync(resident.Id, CondominiumId, actor.Id, default);

        result.Error.Should().Be(ResidentAccessErrors.Forbidden);
        NothingWasQueued();
    }

    // ---- Apoio ----

    private User GivenManager()
    {
        User manager = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager, "sindica@example.com", CondominiumId);
        GivenAccount(manager);

        return manager;
    }

    private User GivenResident(AccountStatus status)
    {
        User resident = UserBuilder.InStatus(status, UserRole.Resident, "maria@exemplo.com", CondominiumId);
        GivenAccount(resident);

        return resident;
    }

    private void GivenAccount(User user) =>
        _accounts.Setup(a => a.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    private void GivenLatestLink(User user, DateTimeOffset issuedAt) =>
        _verifications
            .Setup(v => v.FindLatestOfUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmailVerificationToken.IssueInvitation(user.Id, "hash", issuedAt));

    private void NothingWasRegistered() =>
        _verifications.Verify(v => v.TryRegisterAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);

    private void NothingWasQueued() =>
        _dispatcher.Verify(d => d.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
}
