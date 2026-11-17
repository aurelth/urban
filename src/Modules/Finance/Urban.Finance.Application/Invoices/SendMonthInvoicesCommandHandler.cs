using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

public sealed class SendMonthInvoicesCommandHandler(
    IInvoiceDeliveries deliveries, IInvoiceParties parties, IInvoiceDeliveryDispatcher dispatcher, ITenantContext tenant, IClock clock)
    : IRequestHandler<SendMonthInvoicesCommand, Result<SendMonthInvoicesResult>>
{
    public async Task<Result<SendMonthInvoicesResult>> Handle(SendMonthInvoicesCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<SendMonthInvoicesResult>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        if (month.IsFailure)
        {
            return Result.Failure<SendMonthInvoicesResult>(month.Error);
        }

        if (await deliveries.ListChargesAsync(month.Value, cancellationToken) is not { } charges)
        {
            return Result.Failure<SendMonthInvoicesResult>(ClosingErrors.NotClosed);
        }

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyDictionary<Guid, InvoiceDelivery> existing = await deliveries.ListAsync([.. charges.Select(charge => charge.ChargeId)], cancellationToken);
        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles =
            await parties.GetResponsiblesAsync([.. charges.Select(charge => charge.UnitId).Distinct()], FinanceCalendar.Today(now), cancellationToken);

        List<InvoiceDelivery> created = [];
        List<InvoiceDelivery> requeued = [];
        int alreadySent = 0;
        int alreadyQueued = 0;
        int withoutRecipient = 0;

        foreach (ClosingCharge charge in charges)
        {
            existing.TryGetValue(charge.ChargeId, out InvoiceDelivery? delivery);
            bool hasEmail = responsibles.TryGetValue(charge.UnitId, out InvoiceResponsible? responsible) && responsible.Email is not null;

            switch (InvoiceDeliveryStates.Of(delivery, hasEmail, now))
            {
                case InvoiceDeliveryState.Sent:
                    alreadySent++;
                    break;
                case InvoiceDeliveryState.Queued:
                    alreadyQueued++;
                    break;
                case InvoiceDeliveryState.NoRecipient:
                    withoutRecipient++;
                    break;
                default:
                    if (delivery is null)
                    {
                        created.Add(InvoiceDelivery.Queue(condominiumId, charge.ChargeId, request.ActorUserId, now));
                    }
                    else
                    {
                        // O estado já garantiu que o envio não está na fila, então o pedido novo sempre passa.
                        _ = delivery.Requeue(request.ActorUserId, now);
                        requeued.Add(delivery);
                    }

                    break;
            }
        }

        if (created.Count > 0 && await deliveries.AddAsync(created, cancellationToken) is { IsFailure: true } refused)
        {
            return Result.Failure<SendMonthInvoicesResult>(refused.Error);
        }

        foreach (InvoiceDelivery delivery in requeued)
        {
            await deliveries.SaveAsync(delivery, cancellationToken);
        }

        foreach (InvoiceDelivery delivery in created.Concat(requeued))
        {
            await dispatcher.EnqueueAsync(condominiumId, delivery.ChargeId, cancellationToken);
        }

        return Result.Success(new SendMonthInvoicesResult(created.Count + requeued.Count, alreadySent, alreadyQueued, withoutRecipient));
    }
}
