using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Expenses;

/// <summary>A lista de despesas do condomínio. "Encerrada" é a que tem último mês antes do mês atual.</summary>
public sealed class ListExpensesQueryHandler(IExpenses expenses, ITenantContext tenant, IClock clock)
    : IRequestHandler<ListExpensesQuery, Result<IReadOnlyList<ExpenseView>>>
{
    public async Task<Result<IReadOnlyList<ExpenseView>>> Handle(ListExpensesQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<IReadOnlyList<ExpenseView>>(FinanceErrors.CondominiumNotFound);
        }

        YearMonth reference = FinanceCalendar.CurrentMonth(clock.UtcNow);
        IReadOnlyList<ExpenseRecord> all = await expenses.ListAsync(cancellationToken);

        IEnumerable<ExpenseRecord> chosen = request.Status switch
        {
            ExpenseStatusFilter.Active => all.Where(record => !record.Expense.IsEnded(reference)),
            ExpenseStatusFilter.Ended => all.Where(record => record.Expense.IsEnded(reference)),
            _ => all,
        };

        return Result.Success<IReadOnlyList<ExpenseView>>([.. chosen.Select(record => ExpenseView.From(record, reference))]);
    }
}
