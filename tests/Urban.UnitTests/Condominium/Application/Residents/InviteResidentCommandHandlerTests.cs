using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Condominium.Application.Residents;

/// <summary>
/// O convite cria a conta do morador no Identity e a liga à pessoa na mesma transação; o e-mail só é pedido depois que ela foi
/// confirmada, porque a tarefa em segundo plano pode rodar antes do commit e não acharia a conta.
/// </summary>
public class InviteResidentCommandHandlerTests
{
    private static readonly Guid ActorId = Guid.CreateVersion7();

    private readonly ResidentsTestKit _kit = new();
    private readonly Mock<IResidentAccess> _access = new();
    private readonly RecordingAtomicWork _atomic = new();
    private readonly Guid _newUserId = Guid.CreateVersion7();
    private Result? _outcomeWhenTheEmailWasRequested;

    public InviteResidentCommandHandlerTests()
    {
        _access
            .Setup(a => a.CreateAsync(_kit.CondominiumId, It.IsAny<string>(), ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_newUserId));
        _access
            .Setup(a => a.SendInvitationAsync(It.IsAny<Guid>(), _kit.CondominiumId, ActorId, It.IsAny<CancellationToken>()))
            .Callback(() => _outcomeWhenTheEmailWasRequested = _atomic.Outcome)
            .ReturnsAsync(Result.Success());
    }

    private InviteResidentCommandHandler Handler() => new(_kit.Residents.Object, _access.Object, _atomic, _kit.Tenant, _kit.Clock.Object);

    private static InviteResidentCommand Command(Guid residentId) => new(ActorId, residentId);

    [Fact]
    public async Task Handle_ForAResidentWithoutAnAccount_CreatesItLinksItToThePersonAndThenAsksForTheEmail()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy(_kit.NewResident(email: "maria@exemplo.com"));

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.IsSuccess.Should().BeTrue();
        _access.Verify(a => a.CreateAsync(_kit.CondominiumId, "maria@exemplo.com", ActorId, It.IsAny<CancellationToken>()), Times.Once);
        record.Person.UserId.Should().Be(_newUserId);
        _kit.Saved.Should().ContainSingle().Which.Person.Should().BeSameAs(record.Person);
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Resident);
        entry.SubjectId.Should().Be(record.Person.Id);
        entry.ActorUserId.Should().Be(ActorId);
        entry.At.Should().Be(ResidentsTestKit.Now);
        entry.Fields.Should().Equal("access");
        _access.Verify(a => a.SendInvitationAsync(_newUserId, _kit.CondominiumId, ActorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CreatesTheAccountAndSavesThePersonInsideOneAtomicWork_AndAsksForTheEmailOnlyAfterItEnded()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();

        await Handler().Handle(Command(record.Person.Id), default);

        _atomic.Runs.Should().Be(1);
        _outcomeWhenTheEmailWasRequested.Should().NotBeNull("o e-mail só é pedido depois do commit");
        _outcomeWhenTheEmailWasRequested!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ForAResidentWhoAlreadyHasAnAccount_OnlyResendsTheInvitation()
    {
        Person person = _kit.NewResident();
        Guid existing = Guid.CreateVersion7();
        person.AttachUser(existing);
        ResidentRecord record = _kit.ExistingWithActiveOccupancy(person);

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.IsSuccess.Should().BeTrue();
        _atomic.Runs.Should().Be(0);
        _access.Verify(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _kit.Saved.Should().BeEmpty();
        _access.Verify(a => a.SendInvitationAsync(existing, _kit.CondominiumId, ActorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTheResendIsRefused_ReturnsTheRefusal()
    {
        Person person = _kit.NewResident();
        person.AttachUser(Guid.CreateVersion7());
        ResidentRecord record = _kit.ExistingWithActiveOccupancy(person);
        Error refusal = new("identity.resident_access.not_pending", "recusado");
        _access
            .Setup(a => a.SendInvitationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(refusal));

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.Error.Should().Be(refusal);
    }

    [Fact]
    public async Task Handle_ForAResidentWithoutEmail_FailsAndTouchesNothing()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy(_kit.NewResident(email: null));

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.Error.Should().Be(ResidentErrors.EmailRequiredForInvitation);
        _atomic.Runs.Should().Be(0);
        _access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheIdentityRefusesTheAccount_FailsWithItsErrorAndLinksNothing()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        Error unavailable = new("identity.resident_access.email_unavailable", "indisponível");
        _access
            .Setup(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>(unavailable));

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.Error.Should().Be(unavailable);
        record.Person.UserId.Should().BeNull();
        _kit.Saved.Should().BeEmpty();
        _atomic.Outcome!.IsFailure.Should().BeTrue();
        NoEmailWasRequested();
    }

    [Fact]
    public async Task Handle_WhenSavingThePersonFails_FailsSoTheAtomicWorkRollsTheAccountBack()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        _kit.Residents
            .Setup(r => r.SaveAsync(It.IsAny<ResidentRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(CondominiumErrors.ConcurrencyConflict));

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _atomic.Outcome!.IsFailure.Should().BeTrue();
        NoEmailWasRequested();
    }

    [Fact]
    public async Task Handle_WhenTheEmailCannotBeQueuedAfterTheCommit_ReportsItButTheAccountStays_SoTheManagerCanResend()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();
        Error tooSoon = new("identity.resident_access.resend_too_soon", "cedo demais");
        _access
            .Setup(a => a.SendInvitationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(tooSoon));

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.Error.Should().Be(tooSoon);
        _atomic.Outcome!.IsSuccess.Should().BeTrue();
        record.Person.UserId.Should().Be(_newUserId);
    }

    [Fact]
    public async Task Handle_ForAnUnknownResident_FailsAsNotFound()
    {
        Result result = await Handler().Handle(Command(Guid.CreateVersion7()), default);

        result.Error.Should().Be(ResidentErrors.NotFound);
        _access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WithoutACondominiumInTheRequest_FailsAsNotFound()
    {
        _kit.WithoutATenant();
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();

        Result result = await Handler().Handle(Command(record.Person.Id), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
        _access.VerifyNoOtherCalls();
    }

    private void NoEmailWasRequested() =>
        _access.Verify(a => a.SendInvitationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
}
