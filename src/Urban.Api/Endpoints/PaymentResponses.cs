namespace Urban.Api.Endpoints;

/// <param name="ReceivedOn">AAAA-MM-DD.</param>
/// <param name="Method">"pix", "boleto", "transfer", "cash" ou "other".</param>
/// <param name="ReversedAt">Preenchido quando o pagamento foi estornado; ele continua na lista e deixa de contar.</param>
/// <param name="ReceiptNumber">O número do recibo do pagamento, em sequência por condomínio; o recibo existe mesmo depois do estorno.</param>
/// <param name="ReceiptCode">O número como se fala dele, "REC-000123".</param>
public sealed record PaymentResponse(
    Guid Id,
    Guid ChargeId,
    decimal Amount,
    string ReceivedOn,
    string Method,
    string? Note,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? ReversedAt,
    string? ReversalReason,
    int ReceiptNumber,
    string ReceiptCode);

/// <param name="Paid">A soma dos pagamentos não estornados.</param>
/// <param name="Balance">O que falta pagar (valor da cobrança menos o pago).</param>
/// <param name="Status">"open", "partial", "paid" ou "overdue". É calculada a cada leitura.</param>
/// <param name="LastReceivedOn">A data do último recebimento não estornado (AAAA-MM-DD), ou nulo.</param>
public sealed record ChargeSummaryResponse(
    Guid ChargeId, Guid UnitId, string? Block, string Number, decimal Amount, decimal Paid, decimal Balance, string Status, string? LastReceivedOn);

/// <param name="Charged">O total cobrado no mês inteiro.</param>
/// <param name="Received">O total recebido no mês inteiro (sem os estornos), com ou sem filtro.</param>
/// <param name="Total">Quantas cobranças passam pelo filtro de situação, para a paginação.</param>
public sealed record MonthPaymentsResponse(
    string Month, string DueDate, decimal Charged, decimal Received, int Total, int Page, int PageSize, IReadOnlyList<ChargeSummaryResponse> Items);

public sealed record ChargePaymentsResponse(
    Guid ChargeId, string Month, string DueDate, decimal Amount, decimal Paid, decimal Balance, string Status, IReadOnlyList<PaymentResponse> Payments);
