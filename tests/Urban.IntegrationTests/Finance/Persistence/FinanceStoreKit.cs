using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

/// <summary>Um condomínio de teste e atalhos para guardar grupos, despesas e lançamentos de verdade, cada operação numa "requisição" (contexto) nova.</summary>
internal sealed class FinanceStoreKit(FinancePostgresFixture postgres)
{
    public static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    public static YearMonth M(int year, int month) => YearMonth.Create(year, month).Value;

    public FinanceExpenseGroups Groups(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceExpenses Expenses(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceExpenseEntries Entries(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceSettingsStore Settings(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

    public FinanceConsumptionTariffs Tariffs(Guid? tenant = null) => new(postgres.CreateContext(tenant ?? CondominiumId));

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
}
