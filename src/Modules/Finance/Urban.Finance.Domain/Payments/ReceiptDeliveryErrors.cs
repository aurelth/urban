using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

public static class ReceiptDeliveryErrors
{
    public static readonly Error NoRecipient = new("finance.receipt_delivery.no_recipient", "A unidade não tem um responsável com e-mail cadastrado para receber o recibo");

    public static readonly Error PaymentReversed = new("finance.receipt_delivery.payment_reversed", "O pagamento foi estornado: o recibo dele não é enviado por e-mail");
}
