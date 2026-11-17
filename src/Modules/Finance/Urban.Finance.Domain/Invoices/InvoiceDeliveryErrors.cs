using Urban.SharedKernel;

namespace Urban.Finance.Domain.Invoices;

public static class InvoiceDeliveryErrors
{
    public static readonly Error NoRecipient = new("finance.invoice_delivery.no_recipient", "A unidade não tem um responsável com e-mail cadastrado para receber a fatura");

    public static readonly Error AlreadyQueued = new("finance.invoice_delivery.already_queued", "A fatura desta unidade já está na fila de envio");

    public static readonly Error PageInvalid = new("finance.invoice_delivery.page_invalid", "A página deve ser 1 ou maior e o tamanho da página deve ficar entre 1 e 200");
}
