using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

/// <summary>Para quem e de que pagamento o recibo vai por e-mail: o pagamento, a cobrança dele e o responsável da unidade, com o endereço.</summary>
internal sealed record ReceiptTarget(Payment Payment, ChargeDetail Detail, InvoiceResponsible Responsible, string Email)
{
    /// <summary>
    /// Confere o que a fila e o envio conferem do mesmo jeito: o pagamento existe e não foi estornado (o recibo dele fica cancelado,
    /// então não vai por e-mail) e a unidade tem um responsável com e-mail hoje.
    /// </summary>
    public static async Task<Result<ReceiptTarget>> FindAsync(
        IPayments payments, ICharges charges, IInvoiceParties parties, Guid paymentId, DateOnly today, CancellationToken cancellationToken)
    {
        if (await payments.FindAsync(paymentId, cancellationToken) is not { } payment)
        {
            return Result.Failure<ReceiptTarget>(PaymentErrors.NotFound);
        }

        if (payment.IsReversed)
        {
            return Result.Failure<ReceiptTarget>(ReceiptDeliveryErrors.PaymentReversed);
        }

        if (await charges.FindAsync(payment.ChargeId, cancellationToken) is not { } detail)
        {
            return Result.Failure<ReceiptTarget>(PaymentErrors.NotFound);
        }

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await parties.GetResponsiblesAsync([detail.Charge.UnitId], today, cancellationToken);

        if (!responsibles.TryGetValue(detail.Charge.UnitId, out InvoiceResponsible? responsible) || responsible.Email is not { } email)
        {
            return Result.Failure<ReceiptTarget>(ReceiptDeliveryErrors.NoRecipient);
        }

        return Result.Success(new ReceiptTarget(payment, detail, responsible, email));
    }
}
