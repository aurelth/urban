using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceExpenseEntriesTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    [Fact]
    public async Task AddRange_ThenList_ReturnsTheEntriesOfTheMonthWithVersions()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense zeladoria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        Expense obra = await _kit.StoreInstallmentAsync(group, FinanceStoreKit.M(2026, 9), current: 4);
        Expense energy = Expense.CreateVariable(_kit.CondominiumId, group.Id, "Energia elétrica", null, FinanceStoreKit.M(2026, 9), null, FinanceStoreKit.Now).Value;
        await _kit.Expenses().AddAsync(energy, default);
        List<ExpenseEntry> entries =
        [
            zeladoria.GenerateEntry(FinanceStoreKit.M(2026, 11), FinanceStoreKit.Now).Value,
            obra.GenerateEntry(FinanceStoreKit.M(2026, 11), FinanceStoreKit.Now).Value,
            energy.GenerateEntry(FinanceStoreKit.M(2026, 11), FinanceStoreKit.Now).Value,
            zeladoria.GenerateEntry(FinanceStoreKit.M(2026, 10), FinanceStoreKit.Now).Value,
        ];

        Result added = await _kit.Entries().AddRangeAsync(entries, default);
        IReadOnlyList<ExpenseEntryRecord> november = await _kit.Entries().ListAsync(FinanceStoreKit.M(2026, 11), default);

        added.IsSuccess.Should().BeTrue();
        november.Should().HaveCount(3);
        november.Should().OnlyContain(record => record.Version.Length > 0);
        november.Single(record => record.Entry.Description == "Zeladoria").Entry.Amount.Should().Be(300m);
        november.Single(record => record.Entry.Description == "Obra da garagem").Entry.Label.Should().Be("6/6");
        ExpenseEntry pending = november.Single(record => record.Entry.Description == "Energia elétrica").Entry;
        pending.Amount.Should().BeNull();
        pending.IsPending.Should().BeTrue();
        november.Should().OnlyContain(record => record.Entry.CondominiumId == _kit.CondominiumId && record.Entry.GroupId == group.Id);
    }

    [Fact]
    public async Task AddRange_WithAnEntryThatAlreadyExists_FailsAndStoresNothingOfTheBatch()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense zeladoria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        Expense portaria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9), description: "Portaria remota");
        await _kit.StoreEntryAsync(zeladoria, FinanceStoreKit.M(2026, 11));
        List<ExpenseEntry> batch =
        [
            portaria.GenerateEntry(FinanceStoreKit.M(2026, 11), FinanceStoreKit.Now).Value,
            zeladoria.GenerateEntry(FinanceStoreKit.M(2026, 11), FinanceStoreKit.Now).Value,
        ];

        Result result = await _kit.Entries().AddRangeAsync(batch, default);

        result.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await _kit.Entries().ListAsync(FinanceStoreKit.M(2026, 11), default)).Should().ContainSingle();
    }

    [Fact]
    public async Task List_ReturnsOnlyTheTenantEntries()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense zeladoria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        await _kit.StoreEntryAsync(zeladoria, FinanceStoreKit.M(2026, 11));
        FinanceExpenseEntries noTenant = new(postgres.CreateContext(null));

        (await _kit.Entries(Guid.CreateVersion7()).ListAsync(FinanceStoreKit.M(2026, 11), default)).Should().BeEmpty();
        (await noTenant.ListAsync(FinanceStoreKit.M(2026, 11), default)).Should().BeEmpty();
    }

    [Fact]
    public async Task ListForExpenseFrom_ReturnsTheEntriesFromThatMonthOn()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense zeladoria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        Expense portaria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9), description: "Portaria remota");
        foreach (int month in new[] { 9, 10, 11, 12 })
        {
            await _kit.StoreEntryAsync(zeladoria, FinanceStoreKit.M(2026, month));
        }

        await _kit.StoreEntryAsync(portaria, FinanceStoreKit.M(2026, 11));

        IReadOnlyList<ExpenseEntryRecord> list = await _kit.Entries().ListForExpenseFromAsync(zeladoria.Id, FinanceStoreKit.M(2026, 11), default);

        list.Select(record => record.Entry.Month.ToString()).Should().BeEquivalentTo("2026-11", "2026-12");
        list.Should().OnlyContain(record => record.Entry.ExpenseId == zeladoria.Id);
    }

    [Fact]
    public async Task Save_AfterSettingTheAmount_PersistsItAndChangesTheVersion()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense energy = Expense.CreateVariable(_kit.CondominiumId, group.Id, "Energia elétrica", null, FinanceStoreKit.M(2026, 9), null, FinanceStoreKit.Now).Value;
        await _kit.Expenses().AddAsync(energy, default);
        ExpenseEntry entry = await _kit.StoreEntryAsync(energy, FinanceStoreKit.M(2026, 11));
        FinanceExpenseEntries store = _kit.Entries();
        ExpenseEntryRecord record = (await store.FindAsync(entry.Id, default))!;
        record.Entry.SetAmount(412.37m);

        Result<string> saved = await store.SaveAsync(record, default);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(record.Version);
        ExpenseEntry reloaded = (await _kit.Entries().FindAsync(entry.Id, default))!.Entry;
        reloaded.Amount.Should().Be(412.37m);
        reloaded.IsPending.Should().BeFalse();
    }

    [Fact]
    public async Task Save_WithAStaleVersion_FailsWithAConflict()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense zeladoria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        ExpenseEntry entry = await _kit.StoreEntryAsync(zeladoria, FinanceStoreKit.M(2026, 11));
        FinanceExpenseEntries first = _kit.Entries();
        FinanceExpenseEntries second = _kit.Entries();
        ExpenseEntryRecord firstRecord = (await first.FindAsync(entry.Id, default))!;
        ExpenseEntryRecord secondRecord = (await second.FindAsync(entry.Id, default))!;
        firstRecord.Entry.SetAmount(310m);
        secondRecord.Entry.SetAmount(320m);

        (await first.SaveAsync(firstRecord, default)).IsSuccess.Should().BeTrue();
        Result<string> late = await second.SaveAsync(secondRecord, default);

        late.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await _kit.Entries().FindAsync(entry.Id, default))!.Entry.Amount.Should().Be(310m);
    }

    [Fact]
    public async Task Find_InAnotherCondominium_ReturnsNothing()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense zeladoria = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        ExpenseEntry entry = await _kit.StoreEntryAsync(zeladoria, FinanceStoreKit.M(2026, 11));

        (await _kit.Entries(Guid.CreateVersion7()).FindAsync(entry.Id, default)).Should().BeNull();
    }
}
