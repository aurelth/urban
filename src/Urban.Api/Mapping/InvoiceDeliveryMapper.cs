using Urban.Api.Endpoints;
using Urban.Finance.Application.Invoices;

namespace Urban.Api.Mapping;

public static class InvoiceDeliveryMapper
{
    public static SendMonthInvoicesResponse ToResponse(SendMonthInvoicesResult result) =>
        new(result.Queued, result.AlreadySent, result.AlreadyQueued, result.WithoutRecipient);

    public static InvoiceDeliveriesResponse ToResponse(InvoiceDeliveriesView view) => new(
        view.Month,
        view.Total,
        new InvoiceDeliveryCountsResponse(
            view.Counts[InvoiceDeliveryState.NoRecipient],
            view.Counts[InvoiceDeliveryState.NotSent],
            view.Counts[InvoiceDeliveryState.Queued],
            view.Counts[InvoiceDeliveryState.Sent],
            view.Counts[InvoiceDeliveryState.Failed]),
        view.Page,
        view.PageSize,
        [.. view.Items.Select(item => new InvoiceDeliveryResponse(
            item.ChargeId, item.UnitId, item.Block, item.Number, item.ResponsibleName, StateText(item.State), item.SentAt, item.FailureReason))]);

    private static string StateText(InvoiceDeliveryState state) => state switch
    {
        InvoiceDeliveryState.NoRecipient => "noRecipient",
        InvoiceDeliveryState.NotSent => "notSent",
        InvoiceDeliveryState.Queued => "queued",
        InvoiceDeliveryState.Sent => "sent",
        _ => "failed",
    };
}
