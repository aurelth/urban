using FluentAssertions;
using Urban.Finance.Application.Groups;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class ExpenseGroupHandlersTests
{
    private readonly FinanceTestKit _kit = new();

    private ListExpenseGroupsQueryHandler ListHandler() => new(_kit.Groups, _kit.Tenant);

    private EnsureDefaultExpenseGroupCommandHandler EnsureHandler() => new(_kit.Groups, _kit.Tenant, _kit.Clock);

    private CreateExpenseGroupCommandHandler CreateHandler() => new(_kit.Groups, _kit.Tenant, _kit.Clock);

    private RenameExpenseGroupCommandHandler RenameHandler() => new(_kit.Groups, _kit.Tenant);

    private SetExpenseGroupReserveFundBaseCommandHandler ReserveBaseHandler() => new(_kit.Groups, _kit.Tenant);

    // ---- list ----

    [Fact]
    public async Task List_ReturnsTheGroupsWithTheirVersions()
    {
        _kit.NewGroup("Manutenção das garagens");
        _kit.NewGroup("Despesas ordinárias");

        Result<IReadOnlyList<ExpenseGroupView>> result = await ListHandler().Handle(new ListExpenseGroupsQuery(), default);

        result.Value.Select(group => group.Name).Should().Equal("Despesas ordinárias", "Manutenção das garagens");
        result.Value.Should().OnlyContain(group => group.Version == "5");
    }

    [Fact]
    public async Task List_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await ListHandler().Handle(new ListExpenseGroupsQuery(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- ensure default ----

    [Fact]
    public async Task EnsureDefault_WhenMissing_CreatesTheOrdinaryExpensesGroup()
    {
        Result<ExpenseGroupView> result = await EnsureHandler().Handle(new EnsureDefaultExpenseGroupCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(ExpenseGroup.DefaultName);
        result.Value.Version.Should().Be("1");
        _kit.Groups.All.Should().ContainSingle().Which.CondominiumId.Should().Be(_kit.CondominiumId);
    }

    [Fact]
    public async Task EnsureDefault_WhenItAlreadyExists_ReturnsItWithoutCreatingAnother()
    {
        ExpenseGroup existing = _kit.NewGroup(ExpenseGroup.DefaultName);

        Result<ExpenseGroupView> result = await EnsureHandler().Handle(new EnsureDefaultExpenseGroupCommand(), default);

        result.Value.Id.Should().Be(existing.Id);
        _kit.Groups.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnsureDefault_WhenAnotherRequestCreatesItFirst_ReturnsTheOneThatWon()
    {
        ExpenseGroup winner = ExpenseGroup.Create(_kit.CondominiumId, ExpenseGroup.DefaultName, FinanceTestKit.Now).Value;
        _kit.Groups.BeforeAdd = () => _kit.Groups.Existing(winner, 1);

        Result<ExpenseGroupView> result = await EnsureHandler().Handle(new EnsureDefaultExpenseGroupCommand(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(winner.Id);
        _kit.Groups.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task EnsureDefault_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await EnsureHandler().Handle(new EnsureDefaultExpenseGroupCommand(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- create ----

    [Fact]
    public async Task Create_StoresTheGroupInTheRequestCondominium()
    {
        Result<ExpenseGroupView> result = await CreateHandler().Handle(new CreateExpenseGroupCommand("  Manutenção das garagens "), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Manutenção das garagens");
        result.Value.Version.Should().Be("1");
        _kit.Groups.All.Should().ContainSingle().Which.CondominiumId.Should().Be(_kit.CondominiumId);
    }

    [Fact]
    public async Task Create_WithTheNameOfAnExistingGroupIgnoringCase_Fails()
    {
        _kit.NewGroup("Manutenção das garagens");

        Result<ExpenseGroupView> result = await CreateHandler().Handle(new CreateExpenseGroupCommand("MANUTENÇÃO DAS GARAGENS"), default);

        result.Error.Should().Be(ExpenseGroupErrors.NameAlreadyExists);
        _kit.Groups.All.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_WithAnInvalidName_Fails()
    {
        (await CreateHandler().Handle(new CreateExpenseGroupCommand("  "), default)).Error.Should().Be(ExpenseGroupErrors.NameInvalid);
    }

    [Fact]
    public async Task Create_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await CreateHandler().Handle(new CreateExpenseGroupCommand("Garagens"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    // ---- rename ----

    [Fact]
    public async Task Rename_ChangesTheNameAndReturnsTheNewVersion()
    {
        ExpenseGroup group = _kit.NewGroup("Garagens");

        Result<ExpenseGroupView> result = await RenameHandler().Handle(new RenameExpenseGroupCommand(group.Id, "5", " Manutenção das garagens "), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Manutenção das garagens");
        result.Value.Version.Should().Be("6");
        group.Name.Should().Be("Manutenção das garagens");
    }

    [Fact]
    public async Task Rename_KeepingTheSameNameWithAnotherCase_IsAllowed()
    {
        ExpenseGroup group = _kit.NewGroup("Garagens");

        (await RenameHandler().Handle(new RenameExpenseGroupCommand(group.Id, "5", "GARAGENS"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Rename_WithoutTheVersion_Fails()
    {
        ExpenseGroup group = _kit.NewGroup("Garagens");

        (await RenameHandler().Handle(new RenameExpenseGroupCommand(group.Id, " ", "Outro"), default)).Error.Should().Be(FinanceErrors.VersionRequired);
        group.Name.Should().Be("Garagens");
    }

    [Fact]
    public async Task Rename_WithAnOldVersion_FailsWithAConflict_AndChangesNothing()
    {
        ExpenseGroup group = _kit.NewGroup("Garagens");

        Result<ExpenseGroupView> result = await RenameHandler().Handle(new RenameExpenseGroupCommand(group.Id, "4", "Outro"), default);

        result.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        group.Name.Should().Be("Garagens");
    }

    [Fact]
    public async Task Rename_OfAnUnknownGroup_Fails()
    {
        (await RenameHandler().Handle(new RenameExpenseGroupCommand(Guid.CreateVersion7(), "5", "Outro"), default))
            .Error.Should().Be(ExpenseGroupErrors.NotFound);
    }

    [Fact]
    public async Task Rename_ToTheNameOfAnotherGroup_Fails()
    {
        ExpenseGroup group = _kit.NewGroup("Garagens");
        _kit.NewGroup("Elevadores");

        Result<ExpenseGroupView> result = await RenameHandler().Handle(new RenameExpenseGroupCommand(group.Id, "5", "elevadores"), default);

        result.Error.Should().Be(ExpenseGroupErrors.NameAlreadyExists);
        group.Name.Should().Be("Garagens");
    }

    [Fact]
    public async Task Rename_ToAnInvalidName_Fails()
    {
        ExpenseGroup group = _kit.NewGroup("Garagens");

        (await RenameHandler().Handle(new RenameExpenseGroupCommand(group.Id, "5", ""), default)).Error.Should().Be(ExpenseGroupErrors.NameInvalid);
    }

    // ---- reserve fund base ----

    [Fact]
    public async Task List_ShowsWhichGroupsAreTheReserveFundBase()
    {
        _kit.NewGroup("Despesas ordinárias").SetReserveFundBase(true);
        _kit.NewGroup("Manutenção das garagens");

        Result<IReadOnlyList<ExpenseGroupView>> result = await ListHandler().Handle(new ListExpenseGroupsQuery(), default);

        result.Value.Select(group => group.ReserveFundBase).Should().Equal(true, false);
    }

    [Fact]
    public async Task EnsureDefault_CreatesTheGroupAsPartOfTheReserveFundBase()
    {
        Result<ExpenseGroupView> result = await EnsureHandler().Handle(new EnsureDefaultExpenseGroupCommand(), default);

        result.Value.ReserveFundBase.Should().BeTrue();
        _kit.Groups.All.Single().ReserveFundBase.Should().BeTrue();
    }

    [Fact]
    public async Task SetReserveFundBase_TurnsItOnAndReturnsTheNewVersion()
    {
        ExpenseGroup group = _kit.NewGroup("Manutenção das garagens");

        Result<ExpenseGroupView> result = await ReserveBaseHandler().Handle(new SetExpenseGroupReserveFundBaseCommand(group.Id, "5", true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReserveFundBase.Should().BeTrue();
        result.Value.Version.Should().Be("6");
        group.ReserveFundBase.Should().BeTrue();
    }

    [Fact]
    public async Task SetReserveFundBase_TurnsItOff()
    {
        ExpenseGroup group = _kit.NewGroup();
        group.SetReserveFundBase(true);

        Result<ExpenseGroupView> result = await ReserveBaseHandler().Handle(new SetExpenseGroupReserveFundBaseCommand(group.Id, "5", false), default);

        result.Value.ReserveFundBase.Should().BeFalse();
        group.ReserveFundBase.Should().BeFalse();
    }

    [Fact]
    public async Task SetReserveFundBase_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await ReserveBaseHandler().Handle(new SetExpenseGroupReserveFundBaseCommand(Guid.CreateVersion7(), "5", true), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SetReserveFundBase_WithoutTheVersion_Fails(string? version)
    {
        ExpenseGroup group = _kit.NewGroup();

        (await ReserveBaseHandler().Handle(new SetExpenseGroupReserveFundBaseCommand(group.Id, version, true), default)).Error.Should().Be(FinanceErrors.VersionRequired);
        group.ReserveFundBase.Should().BeFalse();
    }

    [Fact]
    public async Task SetReserveFundBase_ForAGroupThatDoesNotExist_Fails()
    {
        (await ReserveBaseHandler().Handle(new SetExpenseGroupReserveFundBaseCommand(Guid.CreateVersion7(), "5", true), default)).Error.Should().Be(ExpenseGroupErrors.NotFound);
    }

    [Fact]
    public async Task SetReserveFundBase_WithAnOldVersion_FailsAndChangesNothing()
    {
        ExpenseGroup group = _kit.NewGroup();

        (await ReserveBaseHandler().Handle(new SetExpenseGroupReserveFundBaseCommand(group.Id, "4", true), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        group.ReserveFundBase.Should().BeFalse();
    }
}
