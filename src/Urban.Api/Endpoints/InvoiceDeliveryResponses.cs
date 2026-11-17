namespace Urban.Api.Endpoints;

/// <param name="Queued">Quantas faturas entraram na fila agora.</param>
/// <param name="AlreadySent">Quantas unidades já tinham recebido e ficaram de fora.</param>
/// <param name="AlreadyQueued">Quantas já estavam na fila e ficaram de fora.</param>
/// <param name="WithoutRecipient">Quantas não têm responsável com e-mail e ficaram de fora: o síndico precisa cadastrar o e-mail.</param>
public sealed record SendMonthInvoicesResponse(int Queued, int AlreadySent, int AlreadyQueued, int WithoutRecipient);

/// <param name="ResponsibleName">O responsável de hoje pela unidade (inquilino antes de proprietário), ou nulo se não há.</param>
/// <param name="State">"noRecipient", "notSent", "queued", "sent" ou "failed".</param>
/// <param name="SentAt">Quando a fatura saiu pela última vez, ou nulo.</param>
/// <param name="FailureReason">Só em "failed": "not_delivered" (o provedor recusou), "no_recipient" ou "invoice_unavailable".</param>
public sealed record InvoiceDeliveryResponse(
    Guid ChargeId, Guid UnitId, string? Block, string Number, string? ResponsibleName, string State, DateTimeOffset? SentAt, string? FailureReason);

public sealed record InvoiceDeliveryCountsResponse(int NoRecipient, int NotSent, int Queued, int Sent, int Failed);

/// <param name="Total">Quantas cobranças tem o mês (uma por unidade), para a paginação.</param>
/// <param name="Counts">Quantas unidades estão em cada situação, no mês inteiro e não só na página.</param>
public sealed record InvoiceDeliveriesResponse(
    string Month, int Total, InvoiceDeliveryCountsResponse Counts, int Page, int PageSize, IReadOnlyList<InvoiceDeliveryResponse> Items);
