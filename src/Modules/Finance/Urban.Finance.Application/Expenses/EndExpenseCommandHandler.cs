using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Expenses;

/// <summary>Encerra uma despesa fixa ou variável depois de um mês. As parceladas e as únicas terminam sozinhas.</summary>
public sealed class EndExpenseCommandHandler(IExpenses expenses, ITenantContext tenant, IClock clock)
    : IRequestHandler<EndExpenseCommand, Result<ExpenseView>>
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
        Result ended = record.Expense.End(last.Value);

        if (ended.IsFailure)
        {
            return Result.Failure<ExpenseView>(ended.Error);
        }

        Result<string> saved = await expenses.SaveAsync(record, cancellationToken);

        return saved.IsSuccess
            ? Result.Success(ExpenseView.From(record.Expense, saved.Value, FinanceCalendar.CurrentMonth(clock.UtcNow)))
            : Result.Failure<ExpenseView>(saved.Error);
    }
}
