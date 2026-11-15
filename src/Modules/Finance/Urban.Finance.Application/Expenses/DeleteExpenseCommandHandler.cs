using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Expenses;

/// <summary>
/// Exclui uma despesa de vez, com o cronograma, os reajustes e todos os lançamentos. Recusa se algum lançamento estiver num mês
/// fechado ou já pago: nesses casos o histórico precisa ficar, e o caminho é encerrar a despesa.
/// </summary>
public sealed class DeleteExpenseCommandHandler(
    IExpenses expenses,
    IExpenseEntries entries,
    IMonthClosings closings,
    ITenantContext tenant,
    IAtomicWork atomicWork) : IRequestHandler<DeleteExpenseCommand, Result>
{
    public async Task<Result> Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        Result<ExpenseRecord> found = await ExpenseLookup.FindForChangeAsync(expenses, request.ExpenseId, request.Version, cancellationToken);

        if (found.IsFailure)
        {
            return Result.Failure(found.Error);
        }

        ExpenseRecord record = found.Value;
        IReadOnlyList<ExpenseEntryRecord> doomed = await entries.ListForExpenseFromAsync(record.Expense.Id, record.Expense.StartMonth, cancellationToken);
        Result removable = await ExpenseEntryGuard.CheckRemovableAsync(
            closings, doomed, ExpenseErrors.DeleteHasClosedMonths, ExpenseErrors.DeleteHasPaidEntries, cancellationToken);

        if (removable.IsFailure)
        {
            return removable;
        }

        Result atomic = await atomicWork.ExecuteAsync(ct => RemoveAsync(record, doomed, ct), cancellationToken);

        return atomic;
    }

    private async Task<Result> RemoveAsync(ExpenseRecord record, IReadOnlyList<ExpenseEntryRecord> doomed, CancellationToken cancellationToken)
    {
        if (doomed.Count > 0)
        {
            Result removed = await entries.RemoveRangeAsync(doomed, cancellationToken);

            if (removed.IsFailure)
            {
                return removed;
            }
        }

        return await expenses.RemoveAsync(record, cancellationToken);
    }
}
