using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

public class RegisterResidentCommandHandlerTests
{
    private readonly ResidentsTestKit _kit = new();

    private RegisterResidentCommandHandler Handler() => new(_kit.Residents.Object, _kit.Units.Object, _kit.Tenant, _kit.Clock.Object, _kit.Views);

    private RegisterResidentCommand Valid(string? email = "maria@exemplo.com", OccupancyKind kind = OccupancyKind.Owner) =>
        ResidentCommands.Valid(_kit.Unit.Id, email: email, kind: kind);

    [Fact]
    public async Task Handle_RegistersTheResidentInTheCondominiumOfTheRequest_WithoutAccount()
    {
        RegisterResidentCommand command = Valid();

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        ResidentView view = result.Value;
        view.FullName.Should().Be("Maria Souza");
        view.MaskedCpf.Should().Be(CpfMask(command.Cpf!));
        view.BirthDate.Should().Be(new DateOnly(1990, 5, 20));
        view.Phone.Should().Be("47998765432");
        view.Email.Should().Be("maria@exemplo.com");
        view.Profession.Should().Be("Engenheira");
        view.EmergencyContact.Should().Be(new EmergencyContactView("Carlos Souza", "Irmão", "4733334444"));
        view.Version.Should().Be("9");
        ResidentOccupancyView occupancy = view.Occupancies.Should().ContainSingle().Subject;
        occupancy.UnitId.Should().Be(_kit.Unit.Id);
        occupancy.UnitBlock.Should().Be("Bloco A");
        occupancy.UnitNumber.Should().Be("101");
        occupancy.Kind.Should().Be(OccupancyKind.Owner);
        occupancy.StartDate.Should().Be(new DateOnly(2024, 3, 1));
        occupancy.EndDate.Should().BeNull();

        Person person = _kit.AddedPeople.Should().ContainSingle().Subject;
        person.CondominiumId.Should().Be(_kit.CondominiumId);
        person.UserId.Should().BeNull();
        person.Address.Should().BeNull();
        Occupancy saved = _kit.AddedOccupancies.Should().ContainSingle().Subject;
        saved.PersonId.Should().Be(person.Id);
        saved.CondominiumId.Should().Be(_kit.CondominiumId);
        view.Id.Should().Be(person.Id);
    }

    [Fact]
    public async Task Handle_LogsOnlyTheFieldNames_WithTheResidentAsSubject()
    {
        RegisterResidentCommand command = Valid();

        Result<ResidentView> result = await Handler().Handle(command, default);

        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(command.ActorUserId);
        entry.Kind.Should().Be(ChangeKind.Resident);
        entry.SubjectId.Should().Be(result.Value.Id);
        entry.At.Should().Be(ResidentsTestKit.Now);
        entry.Fields.Should().BeEquivalentTo(["fullName", "cpf", "birthDate", "phone", "email", "profession", "emergencyContact", "occupancy"]);
        entry.Fields.Should().NotContain(["Maria Souza", command.Cpf!]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithoutEmail_RegistersAnyway_AndDoesNotLogTheEmailField(string? email)
    {
        Result<ResidentView> result = await Handler().Handle(Valid(email), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().BeNull();
        _kit.Logged.Single().Fields.Should().NotContain("email");
    }

    [Fact]
    public async Task Handle_RegistersATenant()
    {
        Result<ResidentView> result = await Handler().Handle(Valid(kind: OccupancyKind.Tenant), default);

        result.Value.Occupancies.Single().Kind.Should().Be(OccupancyKind.Tenant);
    }

    [Fact]
    public async Task Handle_WithoutACondominium_FailsAndTouchesNothing()
    {
        _kit.WithoutATenant();

        Result<ResidentView> result = await Handler().Handle(Valid(), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
        _kit.Residents.Verify(r => r.AddAsync(It.IsAny<Person>(), It.IsAny<Occupancy>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _kit.Units.Verify(u => u.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<string, Func<RegisterResidentCommand, RegisterResidentCommand>, string> InvalidInputs => new()
    {
        { "name", c => c with { FullName = "Maria" }, "condominium.person.full_name_invalid" },
        { "cpf", c => c with { Cpf = "111.111.111-11" }, "condominium.cpf.invalid" },
        { "birth date missing", c => c with { BirthDate = null }, "condominium.birth_date.invalid" },
        { "birth date in the future", c => c with { BirthDate = new DateOnly(2030, 1, 1) }, "condominium.birth_date.invalid" },
        { "under age", c => c with { BirthDate = new DateOnly(2010, 1, 1) }, "condominium.resident.under_age" },
        { "phone", c => c with { Phone = "123" }, "condominium.phone.invalid" },
        { "email", c => c with { Email = "sem-arroba" }, "condominium.email.invalid" },
        { "profession", c => c with { Profession = "x" }, "condominium.profession.invalid" },
        { "emergency name", c => c with { EmergencyContact = c.EmergencyContact with { Name = "Carlos" } }, "condominium.emergency_contact.name_invalid" },
        { "emergency relationship", c => c with { EmergencyContact = c.EmergencyContact with { Relationship = "" } }, "condominium.emergency_contact.relationship_invalid" },
        { "emergency phone", c => c with { EmergencyContact = c.EmergencyContact with { Phone = "1" } }, "condominium.emergency_contact.phone_invalid" },
        { "kind", c => c with { Kind = 0 }, "condominium.occupancy.kind_invalid" },
        { "start date missing", c => c with { StartDate = null }, "condominium.occupancy.start_date_invalid" },
        { "start date too far ahead", c => c with { StartDate = new DateOnly(2028, 1, 1) }, "condominium.occupancy.start_date_invalid" },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Handle_WithInvalidData_FailsWithTheCodeOfTheField_AndNeverSaves(
        string field,
        Func<RegisterResidentCommand, RegisterResidentCommand> break_,
        string code)
    {
        Result<ResidentView> result = await Handler().Handle(break_(Valid()), default);

        result.IsFailure.Should().BeTrue(field);
        result.Error.Code.Should().Be(code, field);
        _kit.AddedPeople.Should().BeEmpty();
        _kit.Units.Verify(u => u.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TheEighteenthBirthdayCountsInBrazilTime()
    {
        // 02:00 UTC de 13/11/2026 ainda é 12/11 no Brasil: quem faz 18 anos no dia 13 ainda não é adulto.
        _kit.Clock.SetupGet(c => c.UtcNow).Returns(new DateTimeOffset(2026, 11, 13, 2, 0, 0, TimeSpan.Zero));
        RegisterResidentCommand command = Valid() with { BirthDate = new DateOnly(2008, 11, 13) };

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.Error.Should().Be(ResidentErrors.UnderAge);
    }

    [Fact]
    public async Task Handle_WithAnUnknownUnit_FailsAsNotFound_AndNeverSaves()
    {
        RegisterResidentCommand command = Valid() with { UnitId = Guid.CreateVersion7() };
        _kit.Units.Setup(u => u.FindAsync(command.UnitId, It.IsAny<CancellationToken>())).ReturnsAsync((UnitRecord?)null);

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.Error.Should().Be(UnitErrors.NotFound);
        _kit.AddedPeople.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithADeactivatedUnit_Fails_AndNeverSaves()
    {
        CondominiumUnit deactivated = UnitBuilders.Deactivated(_kit.CondominiumId, "202");
        _kit.Units.Setup(u => u.FindAsync(deactivated.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new UnitRecord(deactivated, "2"));

        Result<ResidentView> result = await Handler().Handle(Valid() with { UnitId = deactivated.Id }, default);

        result.Error.Should().Be(UnitErrors.Deactivated);
        _kit.AddedPeople.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheCpfBelongsToAnotherPerson_ReturnsTheStoreError()
    {
        _kit.Residents
            .Setup(r => r.AddAsync(It.IsAny<Person>(), It.IsAny<Occupancy>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(PersonErrors.CpfAlreadyRegistered));

        Result<ResidentView> result = await Handler().Handle(Valid(), default);

        result.Error.Should().Be(PersonErrors.CpfAlreadyRegistered);
    }

    [Fact]
    public void ToString_NeverShowsPersonalData()
    {
        RegisterResidentCommand command = Valid();

        string text = command.ToString();

        text.Should().NotContain(command.Cpf!).And.NotContain("Maria").And.NotContain("maria@exemplo.com").And.NotContain("Carlos");
        command.EmergencyContact.ToString().Should().NotContain("Carlos");
    }

    private static string CpfMask(string cpf)
    {
        string digits = new(cpf.Where(char.IsDigit).ToArray());

        return $"***.{digits[3..6]}.{digits[6..9]}-**";
    }
}
