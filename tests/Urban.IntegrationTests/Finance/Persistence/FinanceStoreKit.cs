using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.IntegrationTests.Finance.Persistence;

/// <summary>Um condomínio de teste e atalhos para guardar grupos, despesas e lançamentos de verdade, cada operação numa "requisição" (contexto) nova.</summary>
internal sealed class FinanceStoreKit(FinancePostgresFixture postgres)
{
    public static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    /// <summary>As duas unidades de todo fechamento do kit, as mesmas em todos os meses: 101 pesa 0,6 e A-102 pesa 0,4.</summary>
    public ApportionmentBasis Basis { get; } = new(
        ApportionmentBasisKind.IdealFraction,
        [new ApportionmentUnit(Guid.CreateVersion7(), null, "101", 0.6m), new ApportionmentUnit(Guid.CreateVersion7(), "A", "102", 0.4m)]);

    public static YearMonth M(int year, int month) => YearMonth.Create(year, month).Value;

    public FinanceExpenseGroups Groups(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceExpenses Expenses(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceExpenseEntries Entries(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceSettingsStore Settings(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public PaymentInstructionsStore Instructions(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceConsumptionTariffs Tariffs(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceMeterReadings Readings(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceMonthClosings Closings(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceCharges Charges(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinancePayments Payments(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceOverdueCharges Overdue(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceInvoiceDeliveries InvoiceDeliveries(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public async Task<ExpenseGroup> StoreGroupAsync(string name = "Despesas ordinárias", Guid? condominiumId = null)
    {
        ExpenseGroup group = ExpenseGroup.Create(condominiumId ?? CondominiumId, name, Now).Value;
        (await Groups(condominiumId).AddAsync(group, default)).IsSuccess.Should().BeTrue();

        return group;
    }

    public async Task<Expense> StoreFixedAsync(ExpenseGroup group, YearMonth start, decimal amount = 300m, string description = "Zeladoria")
    {
        Expense expense = Expense.CreateFixed(group.CondominiumId, group.Id, description, "Limpa Tudo", start, null, amount, 10, Now).Value;
        (await Expenses(group.CondominiumId).AddAsync(expense, default)).IsSuccess.Should().BeTrue();

        return expense;
    }

    public async Task<Expense> StoreInstallmentAsync(ExpenseGroup group, YearMonth first, decimal total = 600m, int count = 6, int current = 4)
    {
        Expense expense = Expense.CreateInstallment(group.CondominiumId, group.Id, "Obra da garagem", "Benedett", total, count, first, current, 10, Now).Value;
        (await Expenses(group.CondominiumId).AddAsync(expense, default)).IsSuccess.Should().BeTrue();

        return expense;
    }

    public async Task<ExpenseEntry> StoreEntryAsync(Expense expense, YearMonth month)
    {
        ExpenseEntry entry = expense.GenerateEntry(month, Now).Value;
        (await Entries(expense.CondominiumId).AddRangeAsync([entry], default)).IsSuccess.Should().BeTrue();

        return entry;
    }

    /// <summary>Fecha o mês de verdade: R$ 1.000,00 rateados em duas unidades, 600 (101) e 400 (A-102), e guarda no banco.</summary>
    public async Task<MonthClosing> StoreClosingAsync(YearMonth month, DateOnly? dueDate = null, Guid? condominiumId = null)
    {
        Guid tenant = condominiumId ?? CondominiumId;
        Expense expense = Expense.CreateFixed(tenant, Guid.CreateVersion7(), "Zeladoria", null, month, null, 1000m, 10, Now).Value;
        ExpenseEntry entry = expense.GenerateEntry(month, Now).Value;
        MonthClosing closing = MonthClosing.Close(tenant, month, dueDate ?? month.AddMonths(1).FirstDay.AddDays(9), Guid.CreateVersion7(), Now, [entry], Basis).Value;
        (await Closings(tenant).AddAsync(closing, default)).IsSuccess.Should().BeTrue();

        return closing;
    }

    public static Payment NewPayment(UnitCharge charge, YearMonth month, decimal amount, DateOnly? receivedOn = null, DateTimeOffset? registeredAt = null) =>
        Payment.Register(
            charge.CondominiumId, charge.Id, month, amount, receivedOn ?? new DateOnly(2026, 11, 5), PaymentMethod.Pix, null, Guid.CreateVersion7(),
            registeredAt ?? Now, new DateOnly(2026, 11, 7)).Value;
}
