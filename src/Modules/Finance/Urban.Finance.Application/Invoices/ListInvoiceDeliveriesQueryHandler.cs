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

public sealed class ListInvoiceDeliveriesQueryHandler(IInvoiceDeliveries deliveries, IInvoiceParties parties, ITenantContext tenant, IClock clock)
    : IRequestHandler<ListInvoiceDeliveriesQuery, Result<InvoiceDeliveriesView>>
{
    public const int MaxPageSize = 200;

    public async Task<Result<InvoiceDeliveriesView>> Handle(ListInvoiceDeliveriesQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<InvoiceDeliveriesView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> month = YearMonth.Parse(request.Month);

        if (month.IsFailure)
        {
            return Result.Failure<InvoiceDeliveriesView>(month.Error);
        }

        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            return Result.Failure<InvoiceDeliveriesView>(InvoiceDeliveryErrors.PageInvalid);
        }

        if (await deliveries.ListChargesAsync(month.Value, cancellationToken) is not { } charges)
        {
            return Result.Failure<InvoiceDeliveriesView>(ClosingErrors.NotClosed);
        }

        DateTimeOffset now = clock.UtcNow;
        IReadOnlyDictionary<Guid, InvoiceDelivery> existing = await deliveries.ListAsync([.. charges.Select(charge => charge.ChargeId)], cancellationToken);
        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles =
            await parties.GetResponsiblesAsync([.. charges.Select(charge => charge.UnitId).Distinct()], FinanceCalendar.Today(now), cancellationToken);

        // Uma linha por unidade: o mês inteiro cabe de uma vez, e os totais por situação valem para ele todo, não só para a página.
        List<InvoiceDeliveryView> rows =
        [
            .. charges.Select(charge =>
            {
                existing.TryGetValue(charge.ChargeId, out InvoiceDelivery? delivery);
                responsibles.TryGetValue(charge.UnitId, out InvoiceResponsible? responsible);
                InvoiceDeliveryState state = InvoiceDeliveryStates.Of(delivery, responsible?.Email is not null, now);

                return new InvoiceDeliveryView(
                    charge.ChargeId,
                    charge.UnitId,
                    charge.Block,
                    charge.Number,
                    responsible?.Name,
                    state,
                    delivery?.SentAt,
                    state == InvoiceDeliveryState.Failed ? delivery?.FailureReason : null);
            }),
        ];

        Dictionary<InvoiceDeliveryState, int> counts = Enum.GetValues<InvoiceDeliveryState>().ToDictionary(state => state, state => rows.Count(row => row.State == state));

        return Result.Success(new InvoiceDeliveriesView(
            month.Value.ToString(), rows.Count, counts, request.Page, request.PageSize, [.. rows.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)]));
    }
}
