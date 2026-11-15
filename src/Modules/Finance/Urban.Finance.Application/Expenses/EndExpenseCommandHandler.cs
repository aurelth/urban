using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Expenses;

/// <summary>
/// Encerra uma despesa fixa ou variável. Ela passa a contar como encerrada na hora e os lançamentos dos meses depois do último
/// mês cobrado são apagados; se algum estiver num mês fechado ou já pago, nada muda. As parceladas e as únicas terminam sozinhas.
/// </summary>
public sealed class EndExpenseCommandHandler(
    IExpenses expenses,
    IExpenseEntries entries,
    IMonthClosings closings,
    ITenantContext tenant,
    IAtomicWork atomicWork,
    IClock clock) : IRequestHandler<EndExpenseCommand, Result<ExpenseView>>
{
    public async Task<Result<ExpenseView>> Handle(EndExpenseCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ExpenseView>(FinanceErrors.CondominiumNotFound);
        }

        Result<ExpenseRecord> found = await ExpenseLookup.FindForChangeAsync(expenses, request.ExpenseId, request.Version, cancellationToken);

        if (found.IsFailure)
        {
            return Result.Failure<ExpenseView>(found.Error);
        }

        Result<YearMonth> last = YearMonth.Parse(request.LastMonth);

        if (last.IsFailure)
        {
            return Result.Failure<ExpenseView>(last.Error);
        }

        ExpenseRecord record = found.Value;
        Result valid = record.Expense.ValidateEnd(last.Value);

        if (valid.IsFailure)
        {
            return Result.Failure<ExpenseView>(valid.Error);
        }

        IReadOnlyList<ExpenseEntryRecord> doomed = await entries.ListForExpenseFromAsync(record.Expense.Id, last.Value.AddMonths(1), cancellationToken);
        Result removable = await ExpenseEntryGuard.CheckRemovableAsync(
            closings, doomed, ExpenseErrors.EndHasClosedMonths, ExpenseErrors.EndHasPaidEntries, cancellationToken);

        if (removable.IsFailure)
        {
            return Result.Failure<ExpenseView>(removable.Error);
        }

        return await Atomic.RunAsync(atomicWork, ct => EndAsync(record, last.Value, doomed, ct), cancellationToken);
    }

    private async Task<Result<ExpenseView>> EndAsync(ExpenseRecord record, YearMonth last, IReadOnlyList<ExpenseEntryRecord> doomed, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.UtcNow;
        Result ended = record.Expense.End(last, now);

        if (ended.IsFailure)
        {
            return Result.Failure<ExpenseView>(ended.Error);
        }

        if (doomed.Count > 0)
        {
            Result removed = await entries.RemoveRangeAsync(doomed, cancellationToken);

            if (removed.IsFailure)
            {
                return Result.Failure<ExpenseView>(removed.Error);
            }
        }

        Result<string> saved = await expenses.SaveAsync(record, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ExpenseView.From(record.Expense, saved.Value, FinanceCalendar.CurrentMonth(now)))
            : Result.Failure<ExpenseView>(saved.Error);
    }
}
