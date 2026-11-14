using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

public class UpdateResidentCommandHandlerTests
{
    private readonly ResidentsTestKit _kit = new();

    private UpdateResidentCommandHandler Handler() => new(_kit.Residents.Object, _kit.Access.Object, _kit.Atomic, _kit.Views, _kit.Tenant, _kit.Clock.Object);

    /// <summary>O morador que "já está no banco" e o comando que repete os mesmos dados dele (versão "4").</summary>
    private (ResidentRecord Record, UpdateResidentCommand Same) Existing()
    {
        ResidentRecord record = _kit.ExistingWithActiveOccupancy();

        return (record, ResidentCommands.Update(record.Person.Id, "4", cpf: record.Person.Cpf.Value));
    }

    [Fact]
    public async Task Handle_SavesTheChangedFields_ReturnsTheNewVersion_AndLogsOnlyTheNames()
    {
        (ResidentRecord record, UpdateResidentCommand same) = Existing();
        UpdateResidentCommand command = same with { Phone = "(47) 99111-2222", Profession = "Médica" };

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().Be("47991112222");
        result.Value.Profession.Should().Be("Médica");
        result.Value.Version.Should().Be("10");
        result.Value.Occupancies.Should().HaveCount(1);
        _kit.Saved.Should().ContainSingle().Which.Person.Should().BeSameAs(record.Person);
        ChangeLogEntry entry = _kit.Logged.Should().ContainSingle().Subject;
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(command.ActorUserId);
        entry.Kind.Should().Be(ChangeKind.Resident);
        entry.SubjectId.Should().Be(record.Person.Id);
        entry.At.Should().Be(ResidentsTestKit.Now);
        entry.Fields.Should().BeEquivalentTo(["phone", "profession"]);
    }

    [Fact]
    public async Task Handle_EveryFieldCanChange_AndEachOneIsLoggedByName()
    {
        (ResidentRecord record, UpdateResidentCommand same) = Existing();
        UpdateResidentCommand command = same with
        {
            FullName = "Maria Souza Lima",
            Cpf = CondominiumBuilders.UniqueCpf().Value,
            BirthDate = new DateOnly(1988, 1, 2),
            Phone = "(47) 99111-2222",
            Email = "outro@exemplo.com",
            Profession = "Médica",
            EmergencyContact = new EmergencyContactInput("Paulo Lima", "Esposo", "(47) 99333-4444"),
        };

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _kit.Logged.Single().Fields.Should().BeEquivalentTo(["fullName", "cpf", "birthDate", "phone", "email", "profession", "emergencyContact"]);
        record.Person.FullName.Value.Should().Be("Maria Souza Lima");
        record.Person.Email!.Value.Should().Be("outro@exemplo.com");
        result.Value.EmergencyContact!.Name.Should().Be("Paulo Lima");
        _kit.Logged.Single().Fields.Should().NotContain(["Maria Souza Lima", command.Cpf!]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithoutTheCpf_KeepsTheCurrentOne(string? cpf)
    {
        (ResidentRecord record, UpdateResidentCommand same) = Existing();
        Cpf current = record.Person.Cpf;

        Result<ResidentView> result = await Handler().Handle(same with { Cpf = cpf, Phone = "(47) 99111-2222" }, default);

        result.IsSuccess.Should().BeTrue();
        record.Person.Cpf.Should().Be(current);
        _kit.Logged.Single().Fields.Should().Equal("phone");
    }

    [Fact]
    public async Task Handle_AnEmptyEmailClearsIt_AndIsLoggedAsEmail()
    {
        (ResidentRecord record, UpdateResidentCommand same) = Existing();

        Result<ResidentView> result = await Handler().Handle(same with { Email = "  " }, default);

        result.IsSuccess.Should().BeTrue();
        record.Person.Email.Should().BeNull();
        result.Value.Email.Should().BeNull();
        _kit.Logged.Single().Fields.Should().Equal("email");
    }

    [Fact]
    public async Task Handle_WhenNothingChanged_ReturnsTheSameResident_WithoutSavingAndKeepingTheVersion()
    {
        (_, UpdateResidentCommand same) = Existing();

        Result<ResidentView> result = await Handler().Handle(same, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("4");
        _kit.Residents.Verify(r => r.SaveAsync(It.IsAny<ResidentRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DifferencesOnlyInSpacingAndCaseOfTheEmailAreNotChanges()
    {
        (_, UpdateResidentCommand same) = Existing();

        Result<ResidentView> result = await Handler().Handle(same with { FullName = "  Maria   Souza ", Email = " MARIA@exemplo.com " }, default);

        result.IsSuccess.Should().BeTrue();
        _kit.Saved.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithoutTheVersion_FailsBeforeReadingAnything(string? version)
    {
        (_, UpdateResidentCommand same) = Existing();

        Result<ResidentView> result = await Handler().Handle(same with { Version = version }, default);

        result.Error.Should().Be(CondominiumErrors.VersionRequired);
        _kit.Residents.Verify(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutACondominium_Fails()
    {
        (_, UpdateResidentCommand same) = Existing();
        _kit.WithoutATenant();

        Result<ResidentView> result = await Handler().Handle(same, default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
        _kit.Residents.Verify(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OfAnUnknownResident_FailsAsNotFound()
    {
        _kit.Residents.Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ResidentRecord?)null);

        Result<ResidentView> result = await Handler().Handle(ResidentCommands.Update(Guid.CreateVersion7()), default);

        result.Error.Should().Be(ResidentErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WithAStaleVersion_FailsAsConflict_AndNeverSaves()
    {
        (_, UpdateResidentCommand same) = Existing();

        Result<ResidentView> result = await Handler().Handle(same with { Version = "3", Phone = "(47) 99111-2222" }, default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _kit.Saved.Should().BeEmpty();
    }

    public static TheoryData<string, Func<UpdateResidentCommand, UpdateResidentCommand>, string> InvalidInputs => new()
    {
        { "name", c => c with { FullName = "Maria" }, "condominium.person.full_name_invalid" },
        { "cpf", c => c with { Cpf = "111.111.111-11" }, "condominium.cpf.invalid" },
        { "birth date missing", c => c with { BirthDate = null }, "condominium.birth_date.invalid" },
        { "under age", c => c with { BirthDate = new DateOnly(2010, 1, 1) }, "condominium.resident.under_age" },
        { "phone", c => c with { Phone = "123" }, "condominium.phone.invalid" },
        { "email", c => c with { Email = "sem-arroba" }, "condominium.email.invalid" },
        { "profession", c => c with { Profession = "x" }, "condominium.profession.invalid" },
        { "emergency name", c => c with { EmergencyContact = c.EmergencyContact with { Name = "Carlos" } }, "condominium.emergency_contact.name_invalid" },
        { "emergency relationship", c => c with { EmergencyContact = c.EmergencyContact with { Relationship = "" } }, "condominium.emergency_contact.relationship_invalid" },
        { "emergency phone", c => c with { EmergencyContact = c.EmergencyContact with { Phone = "1" } }, "condominium.emergency_contact.phone_invalid" },
    };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Handle_WithInvalidData_FailsWithTheCodeOfTheField_AndNeverSaves(
        string field,
        Func<UpdateResidentCommand, UpdateResidentCommand> break_,
        string code)
    {
        (_, UpdateResidentCommand same) = Existing();

        Result<ResidentView> result = await Handler().Handle(break_(same), default);

        result.IsFailure.Should().BeTrue(field);
        result.Error.Code.Should().Be(code, field);
        _kit.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TheStoreErrorsComeBackAsTheyAre()
    {
        (_, UpdateResidentCommand same) = Existing();
        UpdateResidentCommand command = same with { Phone = "(47) 99111-2222" };
        _kit.Residents
            .Setup(r => r.SaveAsync(It.IsAny<ResidentRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(PersonErrors.CpfAlreadyRegistered));

        Result<ResidentView> result = await Handler().Handle(command, default);

        result.Error.Should().Be(PersonErrors.CpfAlreadyRegistered);
    }

    [Fact]
    public void ToString_NeverShowsPersonalData()
    {
        (_, UpdateResidentCommand same) = Existing();

        string text = same.ToString();

        text.Should().NotContain(same.Cpf!).And.NotContain("Maria").And.NotContain("maria@exemplo.com").And.NotContain("Carlos");
    }
}
