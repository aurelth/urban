using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Expenses;

public sealed class SetInstallmentAmountCommandHandler(IExpenses expenses, IExpenseEntries entries, ITenantContext tenant, IAtomicWork atomicWork, IClock clock)
    : IRequestHandler<SetInstallmentAmountCommand, Result<ExpenseView>>
{
    public async Task<Result<ExpenseView>> Handle(SetInstallmentAmountCommand request, CancellationToken cancellationToken)
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

        ExpenseRecord record = found.Value;
        IReadOnlyList<ExpenseEntryRecord> existing = await entries.ListForExpenseFromAsync(record.Expense.Id, record.Expense.StartMonth, cancellationToken);
        Dictionary<Guid, decimal?> before = ExpenseLookup.AmountsOf(record.Expense, existing);

        return await Atomic.RunAsync(atomicWork, ct => ApplyAsync(record, request, existing, before, ct), cancellationToken);
    }

    private async Task<Result<ExpenseView>> ApplyAsync(
        ExpenseRecord record,
        SetInstallmentAmountCommand request,
        IReadOnlyList<ExpenseEntryRecord> existing,
        Dictionary<Guid, decimal?> before,
        CancellationToken cancellationToken)
    {
        Result changed = record.Expense.SetInstallmentAmount(request.Number, request.Amount);

        if (changed.IsFailure)
        {
            return Result.Failure<ExpenseView>(changed.Error);
        }

        // A despesa é gravada antes dos lançamentos: a versão dela é conferida primeiro, e o conflito desfaz tudo na unidade atômica.
        Result<string> saved = await expenses.SaveAsync(record, cancellationToken);

        if (saved.IsFailure)
        {
            return Result.Failure<ExpenseView>(saved.Error);
        }

        Result followed = await ExpenseLookup.FollowNewAmountsAsync(entries, record.Expense, existing, before, cancellationToken);

        return followed.IsSuccess
            ? Result.Success(ExpenseView.From(record.Expense, saved.Value, FinanceCalendar.CurrentMonth(clock.UtcNow)))
            : Result.Failure<ExpenseView>(followed.Error);
    }
}
