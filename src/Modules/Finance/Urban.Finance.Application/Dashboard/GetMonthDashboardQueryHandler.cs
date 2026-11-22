using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Months;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Dashboard;

public sealed class GetMonthDashboardQueryHandler(
    IExpenseEntries entries, IMonthClosings closings, IExpenseGroups groups, IPayments payments, ITenantContext tenant, IClock clock)
    : IRequestHandler<GetMonthDashboardQuery, Result<MonthDashboardView>>
{
    public async Task<Result<MonthDashboardView>> Handle(GetMonthDashboardQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MonthDashboardView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        if (month.IsFailure)
        {
            return Result.Failure<MonthDashboardView>(month.Error);
        }

        DateOnly today = FinanceCalendar.Today(clock.UtcNow);
        MonthView expenses = await MonthReader.ReadAsync(entries, closings, groups, month.Value, today, cancellationToken);

        // Uma cobrança por unidade: o mês inteiro cabe numa página só. A situação sai do ChargeAccount, a mesma regra da lista de pagamentos.
        MonthPaymentsPage? page = await payments.ListMonthAsync(month.Value, null, today, 1, int.MaxValue, cancellationToken);

        return Result.Success(new MonthDashboardView(
            month.Value.ToString(),
            today,
            new DashboardExpensesView(expenses.Total, expenses.NeedsAmountCount, expenses.OverdueCount, expenses.Groups),
            page is null ? null : ChargesOf(page, today)));
    }

    private static DashboardChargesView ChargesOf(MonthPaymentsPage page, DateOnly today)
    {
        List<(ChargeStatus Status, decimal Balance)> rows =
        [
            .. page.Items.Select(row => (
                ChargeAccount.StatusOf(row.Amount, row.Paid, page.DueDate, today),
                ChargeAccount.Balance(row.Amount, row.Paid))),
        ];

        return new DashboardChargesView(
            page.DueDate,
            page.Charged,
            page.Received,
            ChargeAccount.Balance(page.Charged, page.Received),
            rows.Count,
            rows.Count(row => row.Status == ChargeStatus.Open),
            rows.Count(row => row.Status == ChargeStatus.Partial),
            rows.Count(row => row.Status == ChargeStatus.Paid),
            rows.Count(row => row.Status == ChargeStatus.Overdue),
            rows.Where(row => row.Status == ChargeStatus.Overdue).Sum(row => row.Balance));
    }
}
