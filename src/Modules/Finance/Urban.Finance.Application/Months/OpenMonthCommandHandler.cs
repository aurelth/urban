using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

/// <summary>
/// Confere quais despesas vencem no mês e quais já têm lançamento, e grava só as que faltam numa gravação única. Se duas
/// aberturas acontecerem juntas, o índice único (despesa + mês) deixa só uma gravar e a outra recebe um conflito para repetir.
/// </summary>
public sealed class OpenMonthCommandHandler(IExpenses expenses, IExpenseEntries entries, IExpenseGroups groups, ITenantContext tenant, IClock clock)
    : IRequestHandler<OpenMonthCommand, Result<MonthView>>
{
    public async Task<Result<MonthView>> Handle(OpenMonthCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MonthView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> parsed = YearMonth.Parse(request.Month);

        if (parsed.IsFailure)
        {
            return Result.Failure<MonthView>(parsed.Error);
        }

        YearMonth month = parsed.Value;
        DateTimeOffset now = clock.UtcNow;
        Result allowed = MonthRules.CanOpen(month, FinanceCalendar.Today(now));

        if (allowed.IsFailure)
        {
            return Result.Failure<MonthView>(allowed.Error);
        }

        IReadOnlyList<ExpenseEntryRecord> existing = await entries.ListAsync(month, cancellationToken);
        HashSet<Guid> alreadyGenerated = [.. existing.Select(record => record.Entry.ExpenseId)];
        IReadOnlyList<ExpenseRecord> all = await expenses.ListAsync(cancellationToken);

        List<ExpenseEntry> missing =
        [
            .. all
                .Where(record => record.Expense.IsDueIn(month) && !alreadyGenerated.Contains(record.Expense.Id))
                .Select(record => record.Expense.GenerateEntry(month, now).Value),
        ];

        if (missing.Count > 0)
        {
            Result stored = await entries.AddRangeAsync(missing, cancellationToken);

            if (stored.IsFailure)
            {
                return Result.Failure<MonthView>(stored.Error);
            }
        }

        return Result.Success(await MonthReader.ReadAsync(entries, groups, month, cancellationToken));
    }
}
