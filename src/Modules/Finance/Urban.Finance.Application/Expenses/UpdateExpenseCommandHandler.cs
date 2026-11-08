using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Expenses;

/// <summary>Muda descrição, fornecedor, grupo e dia do vencimento. Os lançamentos que já existem guardam o que valia na hora em que nasceram.</summary>
public sealed class UpdateExpenseCommandHandler(IExpenses expenses, IExpenseGroups groups, ITenantContext tenant, IClock clock)
    : IRequestHandler<UpdateExpenseCommand, Result<ExpenseView>>
{
    public async Task<Result<ExpenseView>> Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
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

        if (await groups.FindAsync(request.GroupId, cancellationToken) is null)
        {
            return Result.Failure<ExpenseView>(ExpenseGroupErrors.NotFound);
        }

        ExpenseRecord record = found.Value;
        Result edited = record.Expense.Edit(request.Description, request.Supplier, request.GroupId, request.DueDay ?? 0);

        if (edited.IsFailure)
        {
            return Result.Failure<ExpenseView>(edited.Error);
        }

        Result<string> saved = await expenses.SaveAsync(record, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ExpenseView.From(record.Expense, saved.Value, FinanceCalendar.CurrentMonth(clock.UtcNow)))
            : Result.Failure<ExpenseView>(saved.Error);
    }
}
