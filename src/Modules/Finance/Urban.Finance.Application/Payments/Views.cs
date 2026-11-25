using Urban.Finance.Domain.Payments;

namespace Urban.Finance.Application.Payments;

public sealed record PaymentView(
    Guid Id, Guid ChargeId, decimal Amount, DateOnly ReceivedOn, PaymentMethod Method, string? Note, DateTimeOffset RegisteredAt, DateTimeOffset? ReversedAt, string? ReversalReason, int ReceiptNumber);

public sealed record ChargeSummaryView(
    Guid ChargeId, Guid UnitId, string? Block, string Number, decimal Amount, decimal Paid, decimal Balance, ChargeStatus Status, DateOnly? LastReceivedOn);

public sealed record MonthPaymentsView(
    string Month, DateOnly DueDate, decimal Charged, decimal Received, int Total, int Page, int PageSize, IReadOnlyList<ChargeSummaryView> Items);

public sealed record ChargePaymentsView(
    Guid ChargeId, string Month, DateOnly DueDate, decimal Amount, decimal Paid, decimal Balance, ChargeStatus Status, IReadOnlyList<PaymentView> Payments);

internal static class PaymentViews
{
    public static PaymentView Of(Payment payment) => new(
        payment.Id, payment.ChargeId, payment.Amount, payment.ReceivedOn, payment.Method, payment.Note, payment.RegisteredAt, payment.ReversedAt, payment.ReversalReason, payment.ReceiptNumber);
}
