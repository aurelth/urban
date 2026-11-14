using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

/// <summary>
/// Depois do convite o e-mail do cadastro é o da conta: mudá-lo só no cadastro deixaria o morador entrando por um endereço e o
/// síndico vendo outro. Por isso só se corrige enquanto o convite está pendente (a conta ainda não tem senha); com a conta
/// ativa ou desativada o e-mail trava. Os demais dados continuam editáveis.
/// </summary>
public class UpdateResidentEmailLockTests
{
    private static readonly Guid ActorId = Guid.CreateVersion7();

    private readonly ResidentsTestKit _kit = new();

    private UpdateResidentCommandHandler Handler() => new(_kit.Residents.Object, _kit.Access.Object, _kit.Atomic, _kit.Views, _kit.Tenant, _kit.Clock.Object);

    private ResidentRecord WithAccount(ResidentAccessStatus status) =>
        _kit.ExistingWithActiveOccupancy(_kit.WithAccount(_kit.NewResident(email: "maria@exemplo.com"), status));

    private static UpdateResidentCommand EditEmail(ResidentRecord record, string? email) =>
        ResidentCommands.Update(record.Person.Id, "4", ActorId, record.Person.Cpf.Value, email);

    private void GivenTheEmailChangeSucceeds() =>
        _kit.Access
            .Setup(a => a.ChangeInvitedEmailAsync(It.IsAny<Guid>(), _kit.CondominiumId, It.IsAny<string>(), ActorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

    // ---- Conta ativa ou desativada: o e-mail trava ----

    [Theory]
    [InlineData(ResidentAccessStatus.Active)]
    [InlineData(ResidentAccessStatus.Inactive)]
    public async Task Handle_ChangingTheEmailOfAResidentWhoAlreadyChoseThePassword_FailsAndSavesNothing(ResidentAccessStatus status)
    {
        ResidentRecord record = WithAccount(status);

        Result<ResidentView> result = await Handler().Handle(EditEmail(record, "outro@exemplo.com"), default);

        result.Error.Should().Be(ResidentErrors.EmailLocked);
        record.Person.Email!.Value.Should().Be("maria@exemplo.com");
        _kit.Saved.Should().BeEmpty();
        _kit.Atomic.Runs.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ClearingTheEmailOfAResidentWithAnActiveAccount_Fails()
    {
        ResidentRecord record = WithAccount(ResidentAccessStatus.Active);

        Result<ResidentView> result = await Handler().Handle(EditEmail(record, null), default);

        result.Error.Should().Be(ResidentErrors.EmailLocked);
    }

    [Fact]
    public async Task Handle_RepeatingTheSameEmailWithOtherChanges_SavesNormallyWithoutTouchingTheAccount()
    {
        ResidentRecord record = WithAccount(ResidentAccessStatus.Active);
        UpdateResidentCommand command = EditEmail(record, "  MARIA@exemplo.com ") with { Phone = "(47) 99111-2222" };

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _kit.Logged.Single().Fields.Should().Equal("phone");
        _kit.Atomic.Runs.Should().Be(0);
        _kit.Access.Verify(a => a.ChangeInvitedEmailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ChangingTheEmailOfAResidentWithoutAnAccount_StillWorks()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy(_kit.NewResident(email: "maria@exemplo.com"));

        Result<ResidentView> result = await Handler().Handle(EditEmail(record, "outro@exemplo.com"), default);

        result.IsSuccess.Should().BeTrue();
        record.Person.Email!.Value.Should().Be("outro@exemplo.com");
        _kit.Atomic.Runs.Should().Be(0);
    }

    // ---- Convite pendente: o e-mail ainda se corrige ----

    [Fact]
    public async Task Handle_ChangingTheEmailWhileTheInvitationIsPending_ChangesTheAccountAndTheRegistrationInOneAtomicWork()
    {
        ResidentRecord record = WithAccount(ResidentAccessStatus.Invited);
        Guid userId = record.Person.UserId!.Value;
        GivenTheEmailChangeSucceeds();

        Result<ResidentView> result = await Handler().Handle(EditEmail(record, " Outro@Exemplo.com "), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Atomic.Runs.Should().Be(1);
        _kit.Access.Verify(a => a.ChangeInvitedEmailAsync(userId, _kit.CondominiumId, "outro@exemplo.com", ActorId, It.IsAny<CancellationToken>()), Times.Once);
        record.Person.Email!.Value.Should().Be("outro@exemplo.com");
        _kit.Saved.Should().ContainSingle();
        _kit.Logged.Single().Fields.Should().Equal("email");
        result.Value.AccessStatus.Should().Be(ResidentAccessStatus.Invited);
    }

    [Fact]
    public async Task Handle_WhenTheAccountRefusesTheNewEmail_FailsWithItsErrorAndSavesNothing()
    {
        ResidentRecord record = WithAccount(ResidentAccessStatus.Invited);
        Error taken = new("identity.resident_access.email_unavailable", "Já existe uma conta com este e-mail.");
        _kit.Access
            .Setup(a => a.ChangeInvitedEmailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(taken));

        Result<ResidentView> result = await Handler().Handle(EditEmail(record, "outro@exemplo.com"), default);

        result.Error.Should().Be(taken);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ClearingTheEmailWhileTheInvitationIsPending_Fails_BecauseTheInvitationNeedsAnAddress()
    {
        ResidentRecord record = WithAccount(ResidentAccessStatus.Invited);

        Result<ResidentView> result = await Handler().Handle(EditEmail(record, null), default);

        result.Error.Should().Be(ResidentErrors.EmailLocked);
        _kit.Access.Verify(a => a.ChangeInvitedEmailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ChangingOtherDataWhileTheInvitationIsPending_DoesNotTouchTheAccount()
    {
        ResidentRecord record = WithAccount(ResidentAccessStatus.Invited);
        UpdateResidentCommand command = EditEmail(record, "maria@exemplo.com") with { Phone = "(47) 99111-2222" };

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _kit.Atomic.Runs.Should().Be(0);
    }
}
