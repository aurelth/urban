namespace Urban.Finance.Domain.Invoices;

/// <summary>Por que o último envio falhou. Códigos estáveis: a tela os traduz em texto, e nenhum guarda dado pessoal.</summary>
public static class InvoiceDeliveryFailure
{
    /// <summary>Na hora de enviar, a unidade já não tinha responsável com e-mail.</summary>
    public const string NoRecipient = "no_recipient";

    /// <summary>O provedor de e-mail recusou a mensagem ou não respondeu.</summary>
    public const string NotDelivered = "not_delivered";

    /// <summary>A fatura em PDF não pôde ser montada.</summary>
    public const string InvoiceUnavailable = "invoice_unavailable";
}
