using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Condominium;
using Urban.UnitTests.Condominium.Application;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// Tudo o que um handler do financeiro precisa, já ligado: o condomínio da requisição, o relógio (hoje é 7/11/2026), a unidade
/// atômica e as portas em memória. Cada teste monta só o que quer provar.
/// </summary>
internal sealed class FinanceTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    public ITenantContext Tenant { get; private set; }

    public IClock Clock { get; } = new FixedClock(Now);

    public RecordingAtomicWork Atomic { get; } = new();

    public FakeExpenseGroups Groups { get; } = new();

    public FakeExpenses Expenses { get; } = new();

    public FakeExpenseEntries Entries { get; } = new();

    public FakeMonthClosings Closings { get; } = new();

    public FakeApportionmentBasis Basis { get; } = new();

    public FakeFinanceSettings Settings { get; } = new();

    public FakeConsumptionTariffs Tariffs { get; } = new();

    public FakeMeterReadings Readings { get; } = new();

    public FinanceTestKit()
    {
        Tenant = new FixedTenant(CondominiumId);
    }

    public static YearMonth M(int year, int month) => YearMonth.Create(year, month).Value;

    public FinanceTestKit WithoutATenant()
    {
        Tenant = new FixedTenant(null);

        return this;
    }

    public ExpenseGroup NewGroup(string name = "Despesas ordinárias") =>
        Groups.Existing(ExpenseGroup.Create(CondominiumId, name, Now).Value);

    public Expense NewSingle(ExpenseGroup group, YearMonth month, decimal amount = 250m, string description = "Troca da lâmpada") =>
        Expenses.Existing(Expense.CreateSingle(CondominiumId, group.Id, description, null, month, amount, 10, Now).Value);

    public Expense NewFixed(ExpenseGroup group, YearMonth start, decimal amount = 300m, string description = "Zeladoria", YearMonth? end = null) =>
        Expenses.Existing(Expense.CreateFixed(CondominiumId, group.Id, description, null, start, end, amount, 10, Now).Value);

    public Expense NewVariable(ExpenseGroup group, YearMonth start, string description = "Energia elétrica") =>
        Expenses.Existing(Expense.CreateVariable(CondominiumId, group.Id, description, null, start, null, 10, Now).Value);

    public Expense NewInstallment(
        ExpenseGroup group, YearMonth first, decimal total = 600m, int count = 6, int current = 1, string description = "Obra da garagem") =>
        Expenses.Existing(Expense.CreateInstallment(CondominiumId, group.Id, description, null, total, count, first, current, 10, Now).Value);

    public ExpenseEntry NewEntry(Expense expense, YearMonth month, int version = 5) =>
        Entries.Existing(expense.GenerateEntry(month, Now).Value, version);

    /// <summary>Deixa o mês fechado como se o síndico já tivesse fechado: o total é o dos lançamentos que existirem e o rateio é o do <see cref="Basis"/>.</summary>
    public MonthClosing CloseMonth(YearMonth month, DateOnly? dueDate = null)
    {
        IReadOnlyList<ExpenseEntry> entries = [.. Entries.All.Where(entry => entry.Month == month)];
        MonthClosing closing = MonthClosing.Close(
            CondominiumId, month, dueDate ?? month.AddMonths(1).FirstDay.AddDays(9), Guid.CreateVersion7(), Now, entries, Basis.Outcome.Value).Value;

        return Closings.Existing(closing);
    }
}
