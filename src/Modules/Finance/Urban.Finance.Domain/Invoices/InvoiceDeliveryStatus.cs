namespace Urban.Finance.Domain.Invoices;

/// <summary>Em que pé está o envio da fatura de uma cobrança. Gravado como texto, nunca como número.</summary>
public enum InvoiceDeliveryStatus
{
    /// <summary>Na fila do segundo plano (ou sendo tentada, ou esperando a próxima tentativa).</summary>
    Queued = 1,

    /// <summary>O provedor de e-mail aceitou a mensagem.</summary>
    Sent = 2,

    /// <summary>A última tentativa falhou. Pode estar esperando a retentativa da fila ou já ter esgotado todas.</summary>
    Failed = 3,
}
