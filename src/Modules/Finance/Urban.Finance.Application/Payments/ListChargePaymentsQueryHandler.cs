using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Payments;

public sealed class ListChargePaymentsQueryHandler(IPayments payments, ITenantContext tenant, IClock clock)
    : IRequestHandler<ListChargePaymentsQuery, Result<ChargePaymentsView>>
{
    public async Task<Result<ChargePaymentsView>> Handle(ListChargePaymentsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<ChargePaymentsView>(FinanceErrors.CondominiumNotFound);
        }

        if (await payments.FindChargeAsync(request.ChargeId, cancellationToken) is not { } charge)
        {
            return Result.Failure<ChargePaymentsView>(PaymentErrors.ChargeNotFound);
        }

        IReadOnlyList<Payment> all = await payments.ListForChargeAsync(charge.ChargeId, cancellationToken);
        decimal paid = ChargeAccount.Paid(all);

        return Result.Success(new ChargePaymentsView(
            charge.ChargeId,
            charge.Month.ToString(),
            charge.DueDate,
            charge.Amount,
            paid,
            ChargeAccount.Balance(charge.Amount, paid),
            ChargeAccount.StatusOf(charge.Amount, paid, charge.DueDate, FinanceCalendar.Today(clock.UtcNow)),
            [.. all.Select(PaymentViews.Of)]));
    }
}
