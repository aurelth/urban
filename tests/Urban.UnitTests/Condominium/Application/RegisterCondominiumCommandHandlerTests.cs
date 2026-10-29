using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.UnitTests.Condominium.Application;

public class RegisterCondominiumCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICondominiumRegistrations> _registrations = new(MockBehavior.Strict);
    private readonly Mock<IManagerEnrollment> _enrollment = new(MockBehavior.Strict);
    private readonly Mock<IClock> _clock = new();
    private readonly RecordingAtomicWork _atomic = new();
    private readonly List<string> _calls = [];
    private CondominiumEntity? _savedCondominium;
    private Person? _savedManager;

    public RegisterCondominiumCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        GivenFreeCnpj();
        GivenManagerWithoutCondominium();
        GivenSaveSucceeds();
        GivenEnrollmentSucceeds();
    }

    public static TheoryData<string, Func<RegisterCondominiumCommand, RegisterCondominiumCommand>, string> InvalidInputs => new()
    {
        { "manager name", c => c with { Manager = c.Manager with { FullName = "Ana" } }, "condominium.person.full_name_invalid" },
        { "manager cpf", c => c with { Manager = c.Manager with { Cpf = "111.111.111-11" } }, "condominium.cpf.invalid" },
        { "manager birth date missing", c => c with { Manager = c.Manager with { BirthDate = null } }, "condominium.birth_date.invalid" },
        { "manager under age", c => c with { Manager = c.Manager with { BirthDate = new DateOnly(2010, 1, 1) } }, "condominium.birth_date.under_age" },
        { "manager phone", c => c with { Manager = c.Manager with { Phone = "123" } }, "condominium.phone.invalid" },
        { "manager landline", c => c with { Manager = c.Manager with { Phone = "(47) 3333-4444" } }, "condominium.phone.mobile_required" },
        { "manager postal code", c => c with { Manager = c.Manager with { Address = c.Manager.Address with { PostalCode = "123" } } }, "condominium.postal_code.invalid" },
        { "manager state", c => c with { Manager = c.Manager with { Address = c.Manager.Address with { State = "XX" } } }, "condominium.address.state_invalid" },
        { "manager street", c => c with { Manager = c.Manager with { Address = c.Manager.Address with { Street = " " } } }, "condominium.address.street_invalid" },
        { "condominium name", c => c with { Condominium = c.Condominium with { Name = "A" } }, "condominium.name.invalid" },
        { "condominium cnpj", c => c with { Condominium = c.Condominium with { Cnpj = "11.111.111/1111-11" } }, "condominium.cnpj.invalid" },
        { "condominium city", c => c with { Condominium = c.Condominium with { Address = c.Condominium.Address with { City = "" } } }, "condominium.address.city_invalid" },
        { "condominium number", c => c with { Condominium = c.Condominium with { Address = c.Condominium.Address with { Number = null } } }, "condominium.address.number_invalid" },
    };

    [Fact]
    public async Task Handle_WithValidData_SavesThenEnrollsInsideOneAtomicUnit()
    {
        RegisterCondominiumCommand command = CondominiumCommands.Valid();

        Result<RegisteredCondominium> result = await Handler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _atomic.Runs.Should().Be(1);
        _calls.Should().Equal("cnpj-exists", "manager-has-condominium", "save", "enroll");
    }

    [Fact]
    public async Task Handle_ReturnsTheRegisteredCondominiumWithTheCpfMasked()
    {
        RegisterCondominiumCommand command = CondominiumCommands.Valid(cpf: "529.982.247-25");

        RegisteredCondominium registered = (await Handler().Handle(command, CancellationToken.None)).Value;

        registered.CondominiumId.Should().Be(_savedCondominium!.Id);
        registered.Name.Should().Be("Residencial Vila Verde");
        registered.Cnpj.Should().Be(_savedCondominium.Cnpj.Formatted);
        registered.ManagerCpf.Should().Be("***.982.247-**");
        registered.ManagerCpf.Should().NotContain("52998224725");
    }

    [Fact]
    public async Task Handle_SavesTheCondominiumAndTheManagerLinkedToEachOtherAndToTheUser()
    {
        RegisterCondominiumCommand command = CondominiumCommands.Valid();

        await Handler().Handle(command, CancellationToken.None);

        _savedCondominium!.CreatedBy.Should().Be(command.ManagerUserId);
        _savedCondominium.Cnpj.Value.Should().Be(command.Condominium.Cnpj);
        _savedCondominium.Address.City.Should().Be("Joinville");
        _savedCondominium.CreatedAt.Should().Be(Now);
        _savedManager!.CondominiumId.Should().Be(_savedCondominium.Id);
        _savedManager.UserId.Should().Be(command.ManagerUserId);
        _savedManager.Cpf.Value.Should().Be(command.Manager.Cpf);
        _savedManager.FullName.Value.Should().Be("Ana Maria Silva");
        _savedManager.Phone.IsMobile.Should().BeTrue();
        _savedManager.Address.City.Should().Be("Blumenau");
        _savedManager.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_EnrollsTheManagerInTheNewCondominiumWithTheChosenName()
    {
        RegisterCondominiumCommand command = CondominiumCommands.Valid() with { DisplayName = "  Aninha " };

        await Handler().Handle(command, CancellationToken.None);

        _enrollment.Verify(
            e => e.EnrollAsync(command.ManagerUserId, _savedCondominium!.Id, "  Aninha ", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesAMissingDisplayNameOnForIdentityToJudge()
    {
        RegisterCondominiumCommand command = CondominiumCommands.Valid() with { DisplayName = null };

        await Handler().Handle(command, CancellationToken.None);

        _enrollment.Verify(e => e.EnrollAsync(command.ManagerUserId, It.IsAny<Guid>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Handle_WithInvalidData_FailsWithTheFieldErrorBeforeTouchingAnything(
        string field,
        Func<RegisterCondominiumCommand, RegisterCondominiumCommand> corrupt,
        string expectedCode)
    {
        RegisterCondominiumCommand command = corrupt(CondominiumCommands.Valid());

        Result<RegisteredCondominium> result = await Handler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue($"o campo '{field}' é inválido");
        result.Error.Code.Should().Be(expectedCode);
        _atomic.Runs.Should().Be(0);
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithAnInvalidCpf_NeverEchoesTheNumberInTheError()
    {
        RegisterCondominiumCommand command = CondominiumCommands.Valid(cpf: "123.456.789-00");

        Result<RegisteredCondominium> result = await Handler().Handle(command, CancellationToken.None);

        result.Error.Code.Should().Be("condominium.cpf.invalid");
        result.Error.Description.Should().NotContain("12345678900").And.NotContain("123.456.789");
    }

    [Fact]
    public async Task Handle_WhenTheCnpjIsAlreadyRegistered_FailsWithAConflictAndSavesNothing()
    {
        _registrations.Reset();
        GivenCnpjExists();

        Result<RegisteredCondominium> result = await Handler().Handle(CondominiumCommands.Valid(), CancellationToken.None);

        result.Error.Should().Be(CondominiumErrors.CnpjAlreadyRegistered);
        _calls.Should().Equal("cnpj-exists");
        _enrollment.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTheManagerAlreadyRegisteredACondominium_FailsAndSavesNothing()
    {
        _registrations.Reset();
        GivenFreeCnpj();
        GivenManagerHasCondominium();

        Result<RegisteredCondominium> result = await Handler().Handle(CondominiumCommands.Valid(), CancellationToken.None);

        result.Error.Should().Be(CondominiumErrors.ManagerAlreadyRegistered);
        _calls.Should().Equal("cnpj-exists", "manager-has-condominium");
        _enrollment.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenSavingFails_ReturnsThatFailureAndDoesNotEnroll()
    {
        Error conflict = CondominiumErrors.CnpjAlreadyRegistered;
        _registrations.Reset();
        GivenFreeCnpj();
        GivenManagerWithoutCondominium();
        _registrations
            .Setup(r => r.SaveAsync(It.IsAny<CondominiumEntity>(), It.IsAny<Person>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("save"))
            .ReturnsAsync(Result.Failure(conflict));

        Result<RegisteredCondominium> result = await Handler().Handle(CondominiumCommands.Valid(), CancellationToken.None);

        result.Error.Should().Be(conflict);
        _calls.Should().NotContain("enroll");
        _enrollment.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenIdentityRefusesTheEnrollment_ReturnsItsErrorSoTheAtomicUnitRollsBack()
    {
        Error refusal = new("identity.user.not_manager", "Só síndicos");
        _enrollment.Reset();
        _enrollment
            .Setup(e => e.EnrollAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("enroll"))
            .ReturnsAsync(Result.Failure(refusal));

        Result<RegisteredCondominium> result = await Handler().Handle(CondominiumCommands.Valid(), CancellationToken.None);

        result.Error.Should().Be(refusal);
        _atomic.Outcome!.IsFailure.Should().BeTrue("o decorador só confirma a transação quando o trabalho termina em sucesso");
    }

    [Fact]
    public async Task Handle_OnSuccess_TheAtomicUnitSeesASuccess()
    {
        await Handler().Handle(CondominiumCommands.Valid(), CancellationToken.None);

        _atomic.Outcome!.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("2026-10-29T02:30:00+00:00", false)]
    [InlineData("2026-10-29T03:00:00+00:00", true)]
    public async Task Handle_JudgesTheAgeByTheDateInBrazil_NotInUtc(string instant, bool isAdult)
    {
        _clock.SetupGet(c => c.UtcNow).Returns(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture));
        RegisterCondominiumCommand command = CondominiumCommands.Valid();
        command = command with { Manager = command.Manager with { BirthDate = new DateOnly(2008, 10, 29) } };

        Result<RegisteredCondominium> result = await Handler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().Be(isAdult);
        if (!isAdult)
        {
            result.Error.Should().Be(BirthDate.UnderAge);
        }
    }

    [Fact]
    public void TheConflictErrorsHaveStableCodesAndFriendlyTexts()
    {
        CondominiumErrors.CnpjAlreadyRegistered.Code.Should().Be("condominium.cnpj.already_registered");
        CondominiumErrors.CnpjAlreadyRegistered.Description.Should().Be("Este CNPJ já está cadastrado. Peça um convite ao síndico atual");
        CondominiumErrors.ManagerAlreadyRegistered.Code.Should().Be("condominium.manager.already_registered");
        CondominiumErrors.ManagerAlreadyRegistered.Description.Should().NotBeNullOrWhiteSpace();
    }

    private RegisterCondominiumCommandHandler Handler() =>
        new(_registrations.Object, _enrollment.Object, _atomic, _clock.Object);

    private void GivenFreeCnpj() =>
        _registrations
            .Setup(r => r.CnpjExistsAsync(It.IsAny<Cnpj>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("cnpj-exists"))
            .ReturnsAsync(false);

    private void GivenCnpjExists() =>
        _registrations
            .Setup(r => r.CnpjExistsAsync(It.IsAny<Cnpj>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("cnpj-exists"))
            .ReturnsAsync(true);

    private void GivenManagerWithoutCondominium() =>
        _registrations
            .Setup(r => r.ManagerHasCondominiumAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("manager-has-condominium"))
            .ReturnsAsync(false);

    private void GivenManagerHasCondominium() =>
        _registrations
            .Setup(r => r.ManagerHasCondominiumAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("manager-has-condominium"))
            .ReturnsAsync(true);

    private void GivenSaveSucceeds() =>
        _registrations
            .Setup(r => r.SaveAsync(It.IsAny<CondominiumEntity>(), It.IsAny<Person>(), It.IsAny<CancellationToken>()))
            .Callback<CondominiumEntity, Person, CancellationToken>((condominium, manager, _) =>
            {
                _calls.Add("save");
                _savedCondominium = condominium;
                _savedManager = manager;
            })
            .ReturnsAsync(Result.Success());

    private void GivenEnrollmentSucceeds() =>
        _enrollment
            .Setup(e => e.EnrollAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("enroll"))
            .ReturnsAsync(Result.Success());
}
