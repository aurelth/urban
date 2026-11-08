using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Months;

/// <param name="Status">Situação em "hoje": Pending, Paid, Overdue (pendente depois do vencimento) ou Canceled.</param>
/// <param name="NeedsAmount">Verdadeiro enquanto o valor de uma despesa variável não foi informado.</param>
/// <param name="SuggestedAmount">Só nos lançamentos que precisam de valor: o valor do mês anterior da mesma despesa, para a tela sugerir.</param>
public sealed record ExpenseEntryView(
    Guid Id,
    Guid ExpenseId,
    Guid GroupId,
    string Month,
    string Description,
    decimal? Amount,
    string? Label,
    DateOnly DueDate,
    EntryStatus Status,
    bool NeedsAmount,
    decimal? SuggestedAmount,
    string Version)
{
    public static ExpenseEntryView From(ExpenseEntryRecord record, decimal? suggestedAmount, DateOnly today)
    {
        ExpenseEntry entry = record.Entry;

        return new ExpenseEntryView(
            entry.Id,
            entry.ExpenseId,
            entry.GroupId,
            entry.Month.ToString(),
            entry.Description,
            entry.Amount,
            entry.Label,
            entry.DueDate,
            entry.StatusOn(today),
            entry.NeedsAmount,
            entry.NeedsAmount ? suggestedAmount : null,
            record.Version);
    }
}

/// <param name="Total">Soma dos valores do grupo, sem os cancelados.</param>
/// <param name="NeedsAmountCount">Quantos lançamentos do grupo (não cancelados) ainda esperam o valor.</param>
public sealed record GroupTotalView(Guid GroupId, string GroupName, decimal Total, int NeedsAmountCount);

/// <param name="Total">Soma dos valores do mês, sem os cancelados.</param>
/// <param name="NeedsAmountCount">Quantos lançamentos (não cancelados) ainda esperam o valor.</param>
/// <param name="OverdueCount">Quantos lançamentos estão vencidos hoje.</param>
/// <param name="Closing">Nulo enquanto o mês está aberto; no mês fechado, o total congelado e as cobranças por unidade.</param>
public sealed record MonthView(
    string Month,
    IReadOnlyList<ExpenseEntryView> Entries,
    IReadOnlyList<GroupTotalView> Groups,
    decimal Total,
    int NeedsAmountCount,
    int OverdueCount,
    MonthClosingView? Closing);

/// <param name="Weight">O peso da unidade no rateio (fração ideal, área ou 1), como era quando o mês fechou.</param>
public sealed record UnitChargeView(Guid UnitId, string? Block, string Number, decimal Weight, decimal Amount);

/// <param name="Total">Soma dos lançamentos não cancelados quando o mês fechou.</param>
public sealed record MonthClosingView(
    DateTimeOffset ClosedAt,
    DateOnly DueDate,
    decimal Total,
    ApportionmentBasisKind Basis,
    IReadOnlyList<UnitChargeView> Charges)
{
    public static MonthClosingView From(MonthClosing closing) => new(
        closing.ClosedAt,
        closing.DueDate,
        closing.Total,
        closing.BasisKind,
        [.. closing.Charges.Select(charge => new UnitChargeView(charge.UnitId, charge.Block, charge.Number, charge.Weight, charge.Amount))]);
}
