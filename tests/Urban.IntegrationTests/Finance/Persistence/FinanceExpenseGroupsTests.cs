using FluentAssertions;
using Urban.Finance.Infrastructure.Persistence;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceExpenseGroupsTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    [Fact]
    public async Task Add_ThenFind_ReturnsTheGroupWithItsVersion()
    {
        ExpenseGroup group = ExpenseGroup.Create(_kit.CondominiumId, "Manutenção das garagens", FinanceStoreKit.Now).Value;

        Result<string> added = await _kit.Groups().AddAsync(group, default);
        ExpenseGroupRecord? found = await _kit.Groups().FindAsync(group.Id, default);

        added.IsSuccess.Should().BeTrue();
        found.Should().NotBeNull();
        found!.Group.Name.Should().Be("Manutenção das garagens");
        found.Group.CondominiumId.Should().Be(_kit.CondominiumId);
        found.Group.CreatedAt.Should().Be(FinanceStoreKit.Now);
        found.Version.Should().Be(added.Value);
    }

    [Fact]
    public async Task Add_TheSameNameIgnoringCase_InTheSameCondominium_Fails()
    {
        await _kit.StoreGroupAsync("Garagens");
        ExpenseGroup repeated = ExpenseGroup.Create(_kit.CondominiumId, "GARAGENS", FinanceStoreKit.Now).Value;

        Result<string> result = await _kit.Groups().AddAsync(repeated, default);

        result.Error.Should().Be(ExpenseGroupErrors.NameAlreadyExists);
        (await _kit.Groups().FindAsync(repeated.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Add_TheSameNameInAnotherCondominium_IsAllowed()
    {
        await _kit.StoreGroupAsync("Garagens");
        Guid other = Guid.CreateVersion7();

        ExpenseGroup stored = await _kit.StoreGroupAsync("Garagens", other);

        (await _kit.Groups(other).FindAsync(stored.Id, default)).Should().NotBeNull();
    }

    [Fact]
    public async Task IsNameTaken_IgnoresCase_AndTheGroupBeingEdited()
    {
        ExpenseGroup garagens = await _kit.StoreGroupAsync("Garagens");

        (await _kit.Groups().IsNameTakenAsync("garagens", null, default)).Should().BeTrue();
        (await _kit.Groups().IsNameTakenAsync("garagens", garagens.Id, default)).Should().BeFalse();
        (await _kit.Groups().IsNameTakenAsync("Elevadores", null, default)).Should().BeFalse();
        (await _kit.Groups(Guid.CreateVersion7()).IsNameTakenAsync("garagens", null, default)).Should().BeFalse();
    }

    [Fact]
    public async Task List_ReturnsOnlyTheTenantGroups_SortedIgnoringCaseAndAccents()
    {
        await _kit.StoreGroupAsync("Zeladoria");
        await _kit.StoreGroupAsync("água e esgoto");
        await _kit.StoreGroupAsync("Elevadores");
        await _kit.StoreGroupAsync("Outro condomínio", Guid.CreateVersion7());

        IReadOnlyList<ExpenseGroupRecord> list = await _kit.Groups().ListAsync(default);

        list.Select(record => record.Group.Name).Should().Equal("água e esgoto", "Elevadores", "Zeladoria");
    }

    [Fact]
    public async Task WithoutATenant_NothingIsVisible()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync("Garagens");
        FinanceExpenseGroups noTenant = new(postgres.CreateContext(null));

        (await noTenant.ListAsync(default)).Should().BeEmpty();
        (await noTenant.FindAsync(group.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Find_InAnotherCondominium_ReturnsNothing()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync("Garagens");

        (await _kit.Groups(Guid.CreateVersion7()).FindAsync(group.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_AfterRenaming_PersistsTheNameAndChangesTheVersion()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync("Garagens");
        FinanceExpenseGroups store = _kit.Groups();
        ExpenseGroupRecord record = (await store.FindAsync(group.Id, default))!;
        record.Group.Rename("Manutenção das garagens");

        Result<string> saved = await store.SaveAsync(record, default);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(record.Version);
        (await _kit.Groups().FindAsync(group.Id, default))!.Group.Name.Should().Be("Manutenção das garagens");
    }

    [Fact]
    public async Task Save_WithAStaleVersion_FailsWithAConflict()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync("Garagens");
        FinanceExpenseGroups first = _kit.Groups();
        FinanceExpenseGroups second = _kit.Groups();
        ExpenseGroupRecord firstRecord = (await first.FindAsync(group.Id, default))!;
        ExpenseGroupRecord secondRecord = (await second.FindAsync(group.Id, default))!;
        firstRecord.Group.Rename("Primeiro");
        secondRecord.Group.Rename("Segundo");

        (await first.SaveAsync(firstRecord, default)).IsSuccess.Should().BeTrue();
        Result<string> late = await second.SaveAsync(secondRecord, default);

        late.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await _kit.Groups().FindAsync(group.Id, default))!.Group.Name.Should().Be("Primeiro");
    }

    [Fact]
    public async Task Save_RenamingToTheNameOfAnotherGroup_Fails()
    {
        await _kit.StoreGroupAsync("Elevadores");
        ExpenseGroup group = await _kit.StoreGroupAsync("Garagens");
        FinanceExpenseGroups store = _kit.Groups();
        ExpenseGroupRecord record = (await store.FindAsync(group.Id, default))!;
        record.Group.Rename("elevadores");

        (await store.SaveAsync(record, default)).Error.Should().Be(ExpenseGroupErrors.NameAlreadyExists);
    }

    [Fact]
    public async Task TheReserveFundBaseFlag_IsStoredAndCanBeChanged()
    {
        ExpenseGroup group = ExpenseGroup.Create(_kit.CondominiumId, ExpenseGroup.DefaultName, FinanceStoreKit.Now, reserveFundBase: true).Value;
        await _kit.Groups().AddAsync(group, default);

        FinanceExpenseGroups store = _kit.Groups();
        ExpenseGroupRecord stored = (await store.FindAsync(group.Id, default))!;
        stored.Group.ReserveFundBase.Should().BeTrue();
        stored.Group.SetReserveFundBase(false);
        Result<string> saved = await store.SaveAsync(stored, default);

        saved.IsSuccess.Should().BeTrue();
        (await _kit.Groups().FindAsync(group.Id, default))!.Group.ReserveFundBase.Should().BeFalse();
    }

    [Fact]
    public async Task AGroupCreatedWithoutTheFlag_IsNotPartOfTheReserveFundBase()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync("Manutenção das garagens");

        (await _kit.Groups().FindAsync(group.Id, default))!.Group.ReserveFundBase.Should().BeFalse();
    }
}
