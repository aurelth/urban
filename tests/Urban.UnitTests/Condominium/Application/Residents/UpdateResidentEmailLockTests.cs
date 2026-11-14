using FluentAssertions;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

/// <summary>
/// Depois do convite o e-mail do cadastro é o da conta: mudá-lo só no cadastro deixaria o morador entrando por um endereço e o
/// síndico vendo outro. Os demais dados continuam editáveis.
/// </summary>
public class UpdateResidentEmailLockTests
{
    private readonly ResidentsTestKit _kit = new();

    private UpdateResidentCommandHandler Handler() => new(_kit.Residents.Object, _kit.Tenant, _kit.Clock.Object);

    private ResidentRecord InvitedResident()
    {
        Person person = _kit.NewResident(email: "maria@exemplo.com");
        person.AttachUser(Guid.CreateVersion7());

        return _kit.ExistingWithActiveOccupancy(person);
    }

    [Fact]
    public async Task Handle_ChangingTheEmailOfAResidentWithAnAccount_FailsAndSavesNothing()
    {
        ResidentRecord record = InvitedResident();
        UpdateResidentCommand command = ResidentCommands.Update(record.Person.Id, "4", cpf: record.Person.Cpf.Value, email: "outro@exemplo.com");

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.Error.Should().Be(ResidentErrors.EmailLocked);
        record.Person.Email!.Value.Should().Be("maria@exemplo.com");
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ClearingTheEmailOfAResidentWithAnAccount_Fails()
    {
        ResidentRecord record = InvitedResident();
        UpdateResidentCommand command = ResidentCommands.Update(record.Person.Id, "4", cpf: record.Person.Cpf.Value, email: null);

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.Error.Should().Be(ResidentErrors.EmailLocked);
    }

    [Fact]
    public async Task Handle_RepeatingTheSameEmailWithOtherChanges_SavesNormally()
    {
        ResidentRecord record = InvitedResident();
        UpdateResidentCommand command = ResidentCommands.Update(record.Person.Id, "4", cpf: record.Person.Cpf.Value, email: "  MARIA@exemplo.com ") with { Phone = "(47) 99111-2222" };

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _kit.Logged.Single().Fields.Should().Equal("phone");
    }

    [Fact]
    public async Task Handle_ChangingTheEmailOfAResidentWithoutAnAccount_StillWorks()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy(_kit.NewResident(email: "maria@exemplo.com"));
        UpdateResidentCommand command = ResidentCommands.Update(record.Person.Id, "4", cpf: record.Person.Cpf.Value, email: "outro@exemplo.com");

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        record.Person.Email!.Value.Should().Be("outro@exemplo.com");
    }

    [Fact]
    public async Task The_view_tells_the_screen_whether_the_resident_already_has_an_account()
    {
        ResidentRecord invited = InvitedResident();
        ResidentRecord notInvited = _kit.ExistingWithActiveOccupancy(_kit.NewResident("Beto Lima", "beto@exemplo.com"));

        ResidentView withAccount = (await new GetResidentQueryHandler(_kit.Residents.Object, _kit.Tenant).Handle(new GetResidentQuery(invited.Person.Id), default)).Value;
        ResidentView withoutAccount = (await new GetResidentQueryHandler(_kit.Residents.Object, _kit.Tenant).Handle(new GetResidentQuery(notInvited.Person.Id), default)).Value;

        withAccount.HasAccount.Should().BeTrue();
        withoutAccount.HasAccount.Should().BeFalse();
    }
}
