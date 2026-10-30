using System.Reflection;
using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Profile;
using Urban.Condominium.Application.Registration.RegisterCondominium;
using Urban.Condominium.Domain.Condominiums;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using static Urban.UnitTests.Condominium.Application.Profile.ProfileTestData;

namespace Urban.UnitTests.Condominium.Application.Profile;

public class UpdateMyCondominiumCommandHandlerTests
{
    private readonly Mock<ICondominiumProfiles> _profiles = new();
    private readonly Mock<IManagerProfile> _profile = new();
    private readonly Mock<IClock> _clock = new();
    private readonly RecordingAtomicWork _atomic = new();
    private readonly List<string> _calls = [];
    private readonly ManagerCondominium _registration = CondominiumBuilders.Registration(version: "10.20");
    private ChangeLogEntry? _logged;

    public UpdateMyCondominiumCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
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
        _profile
            .Setup(p => p.GetDisplayNameAsync(_registration.Manager.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CurrentDisplayName);
        _profile
            .Setup(p => p.UpdateDisplayNameAsync(_registration.Manager.UserId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("display-name"))
            .ReturnsAsync((Guid _, string? name, CancellationToken _) => Result.Success(name!.Trim()));
    }

    public static TheoryData<string, Func<UpdateMyCondominiumCommand, UpdateMyCondominiumCommand>, string> InvalidInputs => new()
    {
        { "phone", c => c with { Phone = "123" }, "condominium.phone.invalid" },
        { "landline", c => c with { Phone = "(47) 3322-1100" }, "condominium.phone.mobile_required" },
        { "manager postal code", c => c with { ManagerAddress = c.ManagerAddress with { PostalCode = "12" } }, "condominium.postal_code.invalid" },
        { "manager state", c => c with { ManagerAddress = c.ManagerAddress with { State = "XX" } }, "condominium.address.state_invalid" },
        { "condominium city", c => c with { CondominiumAddress = c.CondominiumAddress with { City = " " } }, "condominium.address.city_invalid" },
        { "condominium name", c => c with { CondominiumName = "A" }, "condominium.name.invalid" },
    };

    [Fact]
    public async Task Handle_ChangesAllTheFreeFields_AndReturnsTheNewVersionAndTheNewValues()
    {
        UpdateMyCondominiumCommand command = Same() with
        {
            DisplayName = " Aninha ",
            Phone = "(47) 98888-7777",
            ManagerAddress = OtherAddress(),
            CondominiumName = "Residencial Bela Vista",
            CondominiumAddress = OtherAddress() with { Street = "Rua Nova" },
        };

        Result<MyCondominium> result = await Handler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        MyCondominium mine = result.Value;
        mine.Version.Should().Be("11.21");
        mine.DisplayName.Should().Be("Aninha");
        mine.Manager.Phone.Should().Be("47988887777");
        mine.Manager.Address.Street.Should().Be("Rua das Flores");
        mine.Condominium.Name.Should().Be("Residencial Bela Vista");
        mine.Condominium.Address.Street.Should().Be("Rua Nova");
        _registration.Manager.Phone.Value.Should().Be("47988887777");
        _registration.Condominium.Name.Should().Be("Residencial Bela Vista");
    }

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task Handle_WithInvalidData_FailsWithTheFieldErrorAndSavesNothing(
        string field,
        Func<UpdateMyCondominiumCommand, UpdateMyCondominiumCommand> corrupt,
        string expectedCode)
    {
        Result<MyCondominium> result = await Handler().Handle(corrupt(Same() with { DisplayName = "Aninha" }), default);

        result.IsFailure.Should().BeTrue($"o campo '{field}' é inválido");
        result.Error.Code.Should().Be(expectedCode);
        _calls.Should().NotContain("save").And.NotContain("display-name");
    }

    [Fact]
    public async Task Handle_WhenIdentityRefusesTheDisplayName_FailsAndSavesNothing()
    {
        Error refused = new("identity.display_name.invalid", "Informe como prefere ser chamado");
        _profile
            .Setup(p => p.UpdateDisplayNameAsync(_registration.Manager.UserId, "1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(refused));

        Result<MyCondominium> result = await Handler().Handle(Same() with { DisplayName = "1" }, default);

        result.Error.Should().Be(refused);
        _calls.Should().NotContain("save");
    }

    [Fact]
    public async Task Handle_WithAStaleVersion_FailsWithAConflictAndWritesNothing()
    {
        Result<MyCondominium> result = await Handler().Handle(Same() with { Version = "9.9", Phone = "(47) 98888-7777" }, default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        CondominiumErrors.ConcurrencyConflict.Code.Should().Be("concurrency.conflict");
        _calls.Should().Equal("find");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_WithoutAVersion_FailsBecauseTheEditNeedsToKnowWhatItIsReplacing(string? version)
    {
        Result<MyCondominium> result = await Handler().Handle(Same() with { Version = version }, default);

        result.Error.Should().Be(CondominiumErrors.VersionRequired);
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithoutARegistration_FailsWithNotFound()
    {
        Guid stranger = Guid.CreateVersion7();
        _profiles.Setup(p => p.FindOfManagerAsync(stranger, It.IsAny<CancellationToken>())).ReturnsAsync((ManagerCondominium?)null);

        Result<MyCondominium> result = await Handler().Handle(Same() with { ManagerUserId = stranger }, default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Handle_RunsEverythingInOneAtomicUnit_IdentityBeforeTheSave()
    {
        await Handler().Handle(Same() with { DisplayName = "Aninha", Phone = "(47) 98888-7777" }, default);

        _atomic.Runs.Should().Be(1);
        _calls.Should().Equal("find", "display-name", "save");
    }

    [Fact]
    public async Task Handle_WhenTheSaveFails_TheAtomicUnitFailsSoIdentityIsRolledBackToo()
    {
        _profiles
            .Setup(p => p.SaveAsync(It.IsAny<ManagerCondominium>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<string>(CondominiumErrors.ConcurrencyConflict));

        Result<MyCondominium> result = await Handler().Handle(Same() with { DisplayName = "Aninha" }, default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _atomic.Outcome!.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LogsOnlyTheNamesOfTheFieldsThatChanged_NeverTheirValues()
    {
        await Handler().Handle(Same() with { Phone = "(47) 98888-7777", CondominiumName = "Residencial Bela Vista" }, default);

        _logged!.Kind.Should().Be(ChangeKind.Profile);
        _logged.Fields.Should().Equal("phone", "condominiumName");
        _logged.CondominiumId.Should().Be(_registration.Condominium.Id);
        _logged.ActorUserId.Should().Be(_registration.Manager.UserId);
        _logged.At.Should().Be(Now);
        string.Join('|', _logged.Fields).Should().NotContain("47988887777").And.NotContain("Bela Vista");
    }

    [Fact]
    public async Task Handle_WhenNothingActuallyChanged_SavesNothingAndKeepsTheVersion()
    {
        Result<MyCondominium> result = await Handler().Handle(Same(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("10.20");
        _calls.Should().Equal("find");
    }

    [Fact]
    public void TheCommand_HasNoRoomForTheSensitiveFields()
    {
        string[] names = typeof(UpdateMyCondominiumCommand).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToArray();

        names.Should().NotContain(n => n.Contains("Cnpj") || n.Contains("Cpf") || n.Contains("FullName") || n.Contains("BirthDate") || n.Contains("Password"));
    }

    private UpdateMyCondominiumCommandHandler Handler() => new(_profiles.Object, _profile.Object, _atomic, _clock.Object);

    private UpdateMyCondominiumCommand Same() =>
        new(
            _registration.Manager.UserId,
            _registration.Version,
            CurrentDisplayName,
            Phone(_registration),
            ToInput(_registration.Manager.Address),
            _registration.Condominium.Name,
            ToInput(_registration.Condominium.Address));
}
