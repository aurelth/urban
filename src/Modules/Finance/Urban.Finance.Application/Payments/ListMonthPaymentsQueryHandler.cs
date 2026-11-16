using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Payments;

public sealed class ListMonthPaymentsQueryHandler(IPayments payments, ITenantContext tenant, IClock clock)
    : IRequestHandler<ListMonthPaymentsQuery, Result<MonthPaymentsView>>
{
    public const int MaxPageSize = 200;

    public async Task<Result<MonthPaymentsView>> Handle(ListMonthPaymentsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MonthPaymentsView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        if (month.IsFailure)
        {
            return Result.Failure<MonthPaymentsView>(month.Error);
        }

        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            return Result.Failure<MonthPaymentsView>(PaymentErrors.PageInvalid);
        }

        DateOnly today = FinanceCalendar.Today(clock.UtcNow);

        if (await payments.ListMonthAsync(month.Value, request.Status, today, request.Page, request.PageSize, cancellationToken) is not { } page)
        {
            return Result.Failure<MonthPaymentsView>(ClosingErrors.NotClosed);
        }

        return Result.Success(new MonthPaymentsView(
            month.Value.ToString(),
            page.DueDate,
            page.Charged,
            page.Received,
            page.Total,
            request.Page,
            request.PageSize,
            [.. page.Items.Select(row => ToView(row, page.DueDate, today))]));
    }

    private static ChargeSummaryView ToView(ChargeRow row, DateOnly dueDate, DateOnly today) => new(
        row.ChargeId,
        row.UnitId,
        row.Block,
        row.Number,
        row.Amount,
        row.Paid,
        ChargeAccount.Balance(row.Amount, row.Paid),
        ChargeAccount.StatusOf(row.Amount, row.Paid, dueDate, today),
        row.LastReceivedOn);
}
