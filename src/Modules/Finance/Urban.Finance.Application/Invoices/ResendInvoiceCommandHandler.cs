using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

public sealed class ResendInvoiceCommandHandler(
    IInvoiceDeliveries deliveries, IInvoiceParties parties, IInvoiceDeliveryDispatcher dispatcher, ITenantContext tenant, IClock clock)
    : IRequestHandler<ResendInvoiceCommand, Result>
{
    public async Task<Result> Handle(ResendInvoiceCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        if (await deliveries.FindChargeAsync(request.ChargeId, cancellationToken) is not { } charge)
        {
            return Result.Failure(PaymentErrors.ChargeNotFound);
        }

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await parties.GetResponsiblesAsync([charge.UnitId], FinanceCalendar.Today(now), cancellationToken);

        if (!responsibles.TryGetValue(charge.UnitId, out InvoiceResponsible? responsible) || responsible.Email is null)
        {
            return Result.Failure(InvoiceDeliveryErrors.NoRecipient);
        }

        Result saved = await QueueAsync(condominiumId, charge, request.ActorUserId, now, cancellationToken);

        if (saved.IsFailure)
        {
            return saved;
        }

        await dispatcher.EnqueueAsync(condominiumId, charge.ChargeId, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> QueueAsync(Guid condominiumId, ClosingCharge charge, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await deliveries.FindAsync(charge.ChargeId, cancellationToken) is not { } delivery)
        {
            return await deliveries.AddAsync([InvoiceDelivery.Queue(condominiumId, charge.ChargeId, actorUserId, now)], cancellationToken);
        }

        Result requeued = delivery.Requeue(actorUserId, now);

        if (requeued.IsSuccess)
        {
            await deliveries.SaveAsync(delivery, cancellationToken);
        }

        return requeued;
    }
}
