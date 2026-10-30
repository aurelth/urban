using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Profile;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using static Urban.UnitTests.Condominium.Application.Profile.ProfileTestData;

namespace Urban.UnitTests.Condominium.Application.Profile;

public class UpdateManagerIdentityCommandHandlerTests
{
    private const string Password = "Senha-segura-123";

    private static readonly Error PasswordIncorrect = new("identity.password.incorrect", "Senha incorreta");

    private readonly Mock<ICondominiumProfiles> _profiles = new();
    private readonly Mock<IPasswordConfirmation> _confirmation = new();
    private readonly Mock<IClock> _clock = new();
    private readonly RecordingAtomicWork _atomic = new();
    private readonly List<string> _calls = [];
    private readonly ManagerCondominium _registration = CondominiumBuilders.Registration(version: "10.20");
    private ChangeLogEntry? _logged;

    public UpdateManagerIdentityCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _confirmation
            .Setup(c => c.ConfirmAsync(_registration.Manager.UserId, Password, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("confirm"))
            .ReturnsAsync(Result.Success());
        _profiles
            .Setup(p => p.FindOfManagerAsync(_registration.Manager.UserId, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("find"))
            .ReturnsAsync(_registration);
        _profiles
            .Setup(p => p.SaveAsync(It.IsAny<ManagerCondominium>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<ManagerCondominium, ChangeLogEntry, CancellationToken>((_, entry, _) =>
            {
                _calls.Add("save");
                _logged = entry;
            })
            .ReturnsAsync(Result.Success("11.21"));
    }

    public static TheoryData<string, Func<UpdateManagerIdentityCommand, UpdateManagerIdentityCommand>, string> InvalidFields => new()
    {
        { "full name", c => c with { FullName = "Ana" }, "condominium.person.full_name_invalid" },
        { "cpf", c => c with { Cpf = "111.111.111-11" }, "condominium.cpf.invalid" },
        { "birth date missing", c => c with { BirthDate = null }, "condominium.birth_date.invalid" },
        { "under age", c => c with { BirthDate = new DateOnly(2010, 1, 1) }, "condominium.birth_date.under_age" },
    };

    public static TheoryData<DateOnly, string?> BirthDateLimits => new()
    {
        { new DateOnly(2008, 10, 29), null },
        { new DateOnly(2008, 10, 30), "condominium.birth_date.under_age" },
        { new DateOnly(1906, 10, 29), null },
        { new DateOnly(1906, 10, 28), "condominium.birth_date.invalid" },
    };

    [Fact]
    public async Task Handle_ChangesTheNameTheCpfAndTheBirthDate_AndReturnsTheNewVersion()
    {
        string newCpf = CondominiumBuilders.UniqueCpf().Value;
        UpdateManagerIdentityCommand command = Same() with { FullName = "Ana Paula Souza", Cpf = newCpf, BirthDate = new DateOnly(1990, 6, 1) };

        Result<MyCondominium> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("11.21");
        result.Value.Manager.FullName.Should().Be("Ana Paula Souza");
        result.Value.Manager.BirthDate.Should().Be(new DateOnly(1990, 6, 1));
        _registration.Manager.Cpf.Value.Should().Be(newCpf);
        _registration.Manager.FullName.Value.Should().Be("Ana Paula Souza");
    }

    [Theory]
    [InlineData("identity.password.incorrect")]
    [InlineData("identity.login.locked_out")]
    [InlineData("identity.session.unauthenticated")]
    public async Task Handle_WhenThePasswordIsNotConfirmed_FailsWithThatErrorAndSavesNothing(string code)
    {
        Error refused = code == "identity.password.incorrect" ? PasswordIncorrect : new Error(code, "recusado");
        _confirmation
            .Setup(c => c.ConfirmAsync(_registration.Manager.UserId, "errada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(refused));

        Result<MyCondominium> result = await Handler().Handle(Same() with { Password = "errada", FullName = "Ana Paula Souza" }, default);

        result.Error.Should().Be(refused);
        _calls.Should().NotContain("save");
        _atomic.Runs.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public async Task Handle_WithAnInvalidField_FailsBeforeAskingForThePassword_SoNoAttemptIsSpent(
        string field,
        Func<UpdateManagerIdentityCommand, UpdateManagerIdentityCommand> corrupt,
        string expectedCode)
    {
        Result<MyCondominium> result = await Handler().Handle(corrupt(Same()), default);

        result.IsFailure.Should().BeTrue($"o campo '{field}' é inválido");
        result.Error.Code.Should().Be(expectedCode);
        _confirmation.VerifyNoOtherCalls();
        _calls.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(BirthDateLimits))]
    public async Task Handle_AcceptsAgesFrom18To120_AndRefusesTheRest(DateOnly birthDate, string? expectedCode)
    {
        Result<MyCondominium> result = await Handler().Handle(Same() with { BirthDate = birthDate }, default);

        if (expectedCode is null)
        {
            result.IsSuccess.Should().BeTrue();
        }
        else
        {
            result.Error.Code.Should().Be(expectedCode);
        }
    }

    [Fact]
    public async Task Handle_WhenTheCpfBelongsToAnotherPersonOfTheCondominium_FailsWithTheConflict()
    {
        _profiles
            .Setup(p => p.SaveAsync(It.IsAny<ManagerCondominium>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(PersonErrors.CpfAlreadyRegistered));

        Result<MyCondominium> result = await Handler().Handle(Same() with { Cpf = CondominiumBuilders.UniqueCpf().Value }, default);

        result.Error.Should().Be(PersonErrors.CpfAlreadyRegistered);
        PersonErrors.CpfAlreadyRegistered.Code.Should().Be("condominium.cpf.already_registered");
        _atomic.Outcome!.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithoutACpf_KeepsTheCurrentOne_AndDoesNotLogIt()
    {
        string currentCpf = _registration.Manager.Cpf.Value;

        await Handler().Handle(Same() with { Cpf = null, FullName = "Ana Paula Souza" }, default);

        _registration.Manager.Cpf.Value.Should().Be(currentCpf);
        _logged!.Fields.Should().Equal("fullName");
    }

    [Fact]
    public async Task Handle_LogsWhoChangedWhatAndWhen_ButNeverTheValues()
    {
        string newCpf = CondominiumBuilders.UniqueCpf().Value;

        await Handler().Handle(Same() with { FullName = "Ana Paula Souza", Cpf = newCpf, BirthDate = new DateOnly(1990, 6, 1) }, default);

        _logged!.Kind.Should().Be(ChangeKind.Identity);
        _logged.Fields.Should().Equal("fullName", "birthDate", "cpf");
        _logged.ActorUserId.Should().Be(_registration.Manager.UserId);
        _logged.CondominiumId.Should().Be(_registration.Condominium.Id);
        _logged.At.Should().Be(Now);
        string.Join('|', _logged.Fields).Should().NotContain(newCpf).And.NotContain("Souza").And.NotContain("1990");
    }

    [Fact]
    public async Task Handle_ConfirmsThePasswordOutsideTheAtomicUnit_SoAWrongAttemptIsStillCounted()
    {
        await Handler().Handle(Same() with { FullName = "Ana Paula Souza" }, default);

        _calls.Should().Equal("confirm", "find", "save");
        _atomic.Runs.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithAStaleVersion_FailsWithAConflictAndWritesNothing()
    {
        Result<MyCondominium> result = await Handler().Handle(Same() with { Version = "9.9", FullName = "Ana Paula Souza" }, default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _calls.Should().NotContain("save");
    }

    [Fact]
    public async Task Handle_WithoutAVersion_Fails()
    {
        Result<MyCondominium> result = await Handler().Handle(Same() with { Version = null }, default);

        result.Error.Should().Be(CondominiumErrors.VersionRequired);
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutARegistration_FailsWithNotFound()
    {
        Guid stranger = Guid.CreateVersion7();
        _confirmation.Setup(c => c.ConfirmAsync(stranger, Password, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        _profiles.Setup(p => p.FindOfManagerAsync(stranger, It.IsAny<CancellationToken>())).ReturnsAsync((ManagerCondominium?)null);

        Result<MyCondominium> result = await Handler().Handle(Same() with { ManagerUserId = stranger }, default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenNothingActuallyChanged_SavesNothingAndKeepsTheVersion()
    {
        Result<MyCondominium> result = await Handler().Handle(Same(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("10.20");
        _calls.Should().NotContain("save");
    }

    [Fact]
    public void TheCommand_NeverPrintsThePasswordOrTheCpf()
    {
        string text = Same().ToString();

        text.Should().NotContain(Password).And.NotContain(_registration.Manager.Cpf.Value);
    }

    private UpdateManagerIdentityCommandHandler Handler() => new(_profiles.Object, _confirmation.Object, _atomic, _clock.Object);

    private UpdateManagerIdentityCommand Same() =>
        new(
            _registration.Manager.UserId,
            _registration.Version,
            Password,
            _registration.Manager.FullName.Value,
            _registration.Manager.BirthDate.Value,
            _registration.Manager.Cpf.Value);
}
