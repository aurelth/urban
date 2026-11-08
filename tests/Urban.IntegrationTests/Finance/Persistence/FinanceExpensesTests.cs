using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceExpensesTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    [Fact]
    public async Task Add_ThenFind_RoundTripsAnInstallmentExpenseWithItsSchedule()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync("Manutenção das garagens");
        Expense stored = await _kit.StoreInstallmentAsync(group, FinanceStoreKit.M(2026, 9), total: 100m, count: 3, current: 2);

        ExpenseRecord found = (await _kit.Expenses().FindAsync(stored.Id, default))!;

        Expense expense = found.Expense;
        expense.Kind.Should().Be(ExpenseKind.Installment);
        expense.Description.Should().Be("Obra da garagem");
        expense.Supplier.Should().Be("Benedett");
        expense.GroupId.Should().Be(group.Id);
        expense.CondominiumId.Should().Be(_kit.CondominiumId);
        expense.StartMonth.Should().Be(FinanceStoreKit.M(2026, 9));
        expense.InstallmentCount.Should().Be(3);
        expense.CurrentInstallment.Should().Be(2);
        expense.LastMonth.Should().Be(FinanceStoreKit.M(2026, 10));
        expense.Installments.OrderBy(parcel => parcel.Number).Select(parcel => parcel.Amount).Should().Equal(33.33m, 33.33m, 33.34m);
        found.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Add_ThenFind_RoundTripsAFixedExpenseWithItsHistoryAndEnd()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense fixedExpense = Expense.CreateFixed(_kit.CondominiumId, group.Id, "Seguro", null, FinanceStoreKit.M(2026, 1), FinanceStoreKit.M(2027, 6), 200m, 10, FinanceStoreKit.Now).Value;
        fixedExpense.ChangeAmountFrom(FinanceStoreKit.M(2026, 7), 230.50m);
        await _kit.Expenses().AddAsync(fixedExpense, default);

        Expense expense = (await _kit.Expenses().FindAsync(fixedExpense.Id, default))!.Expense;

        expense.Supplier.Should().BeNull();
        expense.EndMonth.Should().Be(FinanceStoreKit.M(2027, 6));
        expense.AmountFor(FinanceStoreKit.M(2026, 6)).Should().Be(200m);
        expense.AmountFor(FinanceStoreKit.M(2026, 7)).Should().Be(230.50m);
        expense.AmountChanges.Should().HaveCount(2);
    }

    [Fact]
    public async Task Add_ThenFind_RoundTripsASingleAndAVariableExpense()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense single = Expense.CreateSingle(_kit.CondominiumId, group.Id, "Troca da lâmpada", null, FinanceStoreKit.M(2026, 11), 250m, 10, FinanceStoreKit.Now).Value;
        Expense variable = Expense.CreateVariable(_kit.CondominiumId, group.Id, "Energia elétrica", "Enel", FinanceStoreKit.M(2026, 9), null, 10, FinanceStoreKit.Now).Value;
        await _kit.Expenses().AddAsync(single, default);
        await _kit.Expenses().AddAsync(variable, default);

        Expense foundSingle = (await _kit.Expenses().FindAsync(single.Id, default))!.Expense;
        Expense foundVariable = (await _kit.Expenses().FindAsync(variable.Id, default))!.Expense;

        foundSingle.Kind.Should().Be(ExpenseKind.Single);
        foundSingle.AmountFor(FinanceStoreKit.M(2026, 11)).Should().Be(250m);
        foundVariable.Kind.Should().Be(ExpenseKind.Variable);
        foundVariable.AmountFor(FinanceStoreKit.M(2026, 11)).Should().BeNull();
        foundVariable.IsDueIn(FinanceStoreKit.M(2030, 1)).Should().BeTrue();
    }

    [Fact]
    public async Task List_ReturnsOnlyTheTenantExpenses_WithTheirSchedules_SortedByDescription()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9), description: "Zeladoria");
        await _kit.StoreInstallmentAsync(group, FinanceStoreKit.M(2026, 9));
        ExpenseGroup otherGroup = await _kit.StoreGroupAsync("Outro", Guid.CreateVersion7());
        await _kit.StoreFixedAsync(otherGroup, FinanceStoreKit.M(2026, 9), description: "De outro condomínio");

        IReadOnlyList<ExpenseRecord> list = await _kit.Expenses().ListAsync(default);

        list.Select(record => record.Expense.Description).Should().Equal("Obra da garagem", "Zeladoria");
        list[0].Expense.Installments.Should().HaveCount(6);
        list[1].Expense.AmountChanges.Should().HaveCount(1);
    }

    [Fact]
    public async Task Find_InAnotherCondominium_ReturnsNothing()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense expense = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));

        (await _kit.Expenses(Guid.CreateVersion7()).FindAsync(expense.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_AfterChangingTheAmount_PersistsTheNewHistoryAndChangesTheVersion()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense stored = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        FinanceExpenses store = _kit.Expenses();
        ExpenseRecord record = (await store.FindAsync(stored.Id, default))!;
        record.Expense.ChangeAmountFrom(FinanceStoreKit.M(2027, 1), 330m);

        Result<string> saved = await store.SaveAsync(record, default);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(record.Version);
        ExpenseRecord reloaded = (await _kit.Expenses().FindAsync(stored.Id, default))!;
        reloaded.Expense.AmountFor(FinanceStoreKit.M(2027, 1)).Should().Be(330m);
        reloaded.Version.Should().Be(saved.Value);
    }

    [Fact]
    public async Task Save_AfterChangingOnlyAParcel_StillChangesTheVersion()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense stored = await _kit.StoreInstallmentAsync(group, FinanceStoreKit.M(2026, 9), current: 1);
        FinanceExpenses store = _kit.Expenses();
        ExpenseRecord record = (await store.FindAsync(stored.Id, default))!;
        record.Expense.SetInstallmentAmount(2, 120m);

        Result<string> saved = await store.SaveAsync(record, default);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(record.Version);
        (await _kit.Expenses().FindAsync(stored.Id, default))!.Expense.Installments.Single(parcel => parcel.Number == 2).Amount.Should().Be(120m);
    }

    [Fact]
    public async Task Save_TwoEditsOfTheSameParcelFromTheSameVersion_SecondOneFails()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense stored = await _kit.StoreInstallmentAsync(group, FinanceStoreKit.M(2026, 9), current: 1);
        FinanceExpenses first = _kit.Expenses();
        FinanceExpenses second = _kit.Expenses();
        ExpenseRecord firstRecord = (await first.FindAsync(stored.Id, default))!;
        ExpenseRecord secondRecord = (await second.FindAsync(stored.Id, default))!;
        firstRecord.Expense.SetInstallmentAmount(2, 120m);
        secondRecord.Expense.SetInstallmentAmount(2, 130m);

        (await first.SaveAsync(firstRecord, default)).IsSuccess.Should().BeTrue();
        Result<string> late = await second.SaveAsync(secondRecord, default);

        late.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await _kit.Expenses().FindAsync(stored.Id, default))!.Expense.Installments.Single(parcel => parcel.Number == 2).Amount.Should().Be(120m);
    }

    [Fact]
    public async Task Save_TwoReadjustmentsFromTheSameVersion_SecondOneFails_EvenForDifferentMonths()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense stored = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        FinanceExpenses first = _kit.Expenses();
        FinanceExpenses second = _kit.Expenses();
        ExpenseRecord firstRecord = (await first.FindAsync(stored.Id, default))!;
        ExpenseRecord secondRecord = (await second.FindAsync(stored.Id, default))!;
        firstRecord.Expense.ChangeAmountFrom(FinanceStoreKit.M(2027, 1), 330m);
        secondRecord.Expense.ChangeAmountFrom(FinanceStoreKit.M(2027, 3), 360m);

        (await first.SaveAsync(firstRecord, default)).IsSuccess.Should().BeTrue();
        Result<string> late = await second.SaveAsync(secondRecord, default);

        late.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await _kit.Expenses().FindAsync(stored.Id, default))!.Expense.AmountChanges.Should().HaveCount(2);
    }

    [Fact]
    public async Task Save_AfterEditingTheDescription_Persists()
    {
        ExpenseGroup group = await _kit.StoreGroupAsync();
        Expense stored = await _kit.StoreFixedAsync(group, FinanceStoreKit.M(2026, 9));
        FinanceExpenses store = _kit.Expenses();
        ExpenseRecord record = (await store.FindAsync(stored.Id, default))!;
        record.Expense.Edit("Zeladoria e limpeza", null, group.Id, 10);
        record.Expense.End(FinanceStoreKit.M(2027, 2));

        (await store.SaveAsync(record, default)).IsSuccess.Should().BeTrue();

        Expense reloaded = (await _kit.Expenses().FindAsync(stored.Id, default))!.Expense;
        reloaded.Description.Should().Be("Zeladoria e limpeza");
        reloaded.Supplier.Should().BeNull();
        reloaded.EndMonth.Should().Be(FinanceStoreKit.M(2027, 2));
    }
}
