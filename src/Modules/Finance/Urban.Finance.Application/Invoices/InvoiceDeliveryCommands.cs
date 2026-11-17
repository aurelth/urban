using MediatR;
using Urban.SharedKernel;

namespace Urban.Finance.Application.Invoices;

/// <summary>
/// Pede o envio por e-mail da fatura de todas as unidades do mês fechado que ainda não a receberam. Quem já recebeu, quem está na
/// fila e quem não tem e-mail ficam de fora; quem falhou entra de novo. O envio roda em segundo plano.
/// </summary>
public sealed record SendMonthInvoicesCommand(string? Month, Guid ActorUserId) : IRequest<Result<SendMonthInvoicesResult>>;

/// <summary>Pede o envio da fatura de uma unidade, mesmo que ela já tenha sido enviada (reenvio).</summary>
public sealed record ResendInvoiceCommand(Guid ChargeId, Guid ActorUserId) : IRequest<Result>;

/// <summary>Roda no segundo plano: monta a fatura em PDF e a envia ao responsável da unidade. O tenant já foi escolhido por quem chama.</summary>
public sealed record SendInvoiceCommand(Guid ChargeId) : IRequest<Result>;

/// <summary>Como está o envio das faturas de um mês fechado, uma linha por unidade. O mês vai como texto "AAAA-MM".</summary>
public sealed record ListInvoiceDeliveriesQuery(string? Month, int Page = 1, int PageSize = 50) : IRequest<Result<InvoiceDeliveriesView>>;

/// <param name="Queued">Quantas faturas entraram na fila agora.</param>
/// <param name="AlreadySent">Quantas unidades já tinham recebido e ficaram de fora.</param>
/// <param name="AlreadyQueued">Quantas já estavam na fila e ficaram de fora.</param>
/// <param name="WithoutRecipient">Quantas não têm responsável com e-mail e ficaram de fora.</param>
public sealed record SendMonthInvoicesResult(int Queued, int AlreadySent, int AlreadyQueued, int WithoutRecipient);

public sealed record InvoiceDeliveryView(
    Guid ChargeId,
    Guid UnitId,
    string? Block,
    string Number,
    string? ResponsibleName,
    InvoiceDeliveryState State,
    DateTimeOffset? SentAt,
    string? FailureReason);

/// <param name="Total">Quantas cobranças tem o mês (uma por unidade).</param>
/// <param name="Counts">Quantas unidades estão em cada situação, no mês inteiro e não só na página.</param>
public sealed record InvoiceDeliveriesView(
    string Month, int Total, IReadOnlyDictionary<InvoiceDeliveryState, int> Counts, int Page, int PageSize, IReadOnlyList<InvoiceDeliveryView> Items);
