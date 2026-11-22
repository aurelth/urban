using System.Globalization;
using Urban.Api.Endpoints;
using Urban.Finance.Application.Payments;
using Urban.Finance.Domain.Payments;

namespace Urban.Api.Mapping;

/// <summary>Do corpo para o comando e do resultado para a resposta. Datas em AAAA-MM-DD e enums em texto camelCase.</summary>
public static class PaymentMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Data ausente ou fora do formato vira <see cref="DateOnly.MinValue"/>, que o Domain recusa com o código da data na ordem
    /// certa (depois do valor e da forma), sem a API precisar repetir essa conferência.
    /// </summary>
    public static RegisterPaymentCommand ToCommand(RegisterPaymentRequest request, Guid chargeId, Guid actorUserId) =>
        new(chargeId, request.Amount ?? 0m, ParseDate(request.ReceivedOn), ParseMethod(request.Method), request.Note, actorUserId);

    public static ReversePaymentCommand ToCommand(ReversePaymentRequest request, Guid paymentId, Guid actorUserId) =>
        new(paymentId, request.Reason, actorUserId);

    public static DateOnly ParseDate(string? text) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date) ? date : DateOnly.MinValue;

    /// <summary>Texto desconhecido vira um valor fora do enum, que o Domain recusa com o código da forma de pagamento.</summary>
    public static PaymentMethod ParseMethod(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "pix" => PaymentMethod.Pix,
        "boleto" => PaymentMethod.Boleto,
        "transfer" => PaymentMethod.Transfer,
        "cash" => PaymentMethod.Cash,
        "other" => PaymentMethod.Other,
        _ => (PaymentMethod)(-1),
    };

    /// <returns>Falso quando o texto não é uma situação conhecida; vazio é "sem filtro".</returns>
    public static bool TryParseStatus(string? text, out ChargeStatus? status)
    {
        status = text?.Trim().ToLowerInvariant() switch
        {
            null or "" => null,
            "open" => ChargeStatus.Open,
            "partial" => ChargeStatus.Partial,
            "paid" => ChargeStatus.Paid,
            "overdue" => ChargeStatus.Overdue,
            _ => (ChargeStatus?)(-1),
        };

        return status is not (ChargeStatus)(-1);
    }

    public static PaymentResponse ToResponse(PaymentView view) => new(
        view.Id, view.ChargeId, view.Amount, Format(view.ReceivedOn), MethodText(view.Method), view.Note, view.RegisteredAt, view.ReversedAt, view.ReversalReason);

    public static MonthPaymentsResponse ToResponse(MonthPaymentsView view) => new(
        view.Month,
        Format(view.DueDate),
        view.Charged,
        view.Received,
        view.Total,
        view.Page,
        view.PageSize,
        [.. view.Items.Select(item => new ChargeSummaryResponse(
            item.ChargeId, item.UnitId, item.Block, item.Number, item.Amount, item.Paid, item.Balance, StatusText(item.Status), item.LastReceivedOn is { } last ? Format(last) : null))]);

    public static ChargePaymentsResponse ToResponse(ChargePaymentsView view) => new(
        view.ChargeId, view.Month, Format(view.DueDate), view.Amount, view.Paid, view.Balance, StatusText(view.Status), [.. view.Payments.Select(ToResponse)]);

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string MethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Pix => "pix",
        PaymentMethod.Boleto => "boleto",
        PaymentMethod.Transfer => "transfer",
        PaymentMethod.Cash => "cash",
        _ => "other",
    };

    public static string StatusText(ChargeStatus status) => status switch
    {
        ChargeStatus.Open => "open",
        ChargeStatus.Partial => "partial",
        ChargeStatus.Paid => "paid",
        _ => "overdue",
    };
}
