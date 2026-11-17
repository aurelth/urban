using Urban.Finance.Domain.Invoices;

namespace Urban.Finance.Application.Invoices;

/// <summary>A situação do envio da fatura de uma unidade, como o síndico a vê.</summary>
public enum InvoiceDeliveryState
{
    /// <summary>Sem responsável com e-mail: a fatura não tem para onde ir, e o síndico precisa resolver o cadastro.</summary>
    NoRecipient = 1,

    /// <summary>Nunca pedida (ou a fila perdeu o pedido): ainda não saiu.</summary>
    NotSent = 2,

    Queued = 3,

    Sent = 4,

    Failed = 5,
}

public static class InvoiceDeliveryStates
{
    /// <summary>
    /// Enviada e na fila valem antes de tudo, porque já aconteceram ou estão acontecendo; depois, sem e-mail não há o que fazer;
    /// o resto é falha ou ainda não enviada. O mesmo cálculo decide o que o envio do mês faz com cada unidade.
    /// </summary>
    public static InvoiceDeliveryState Of(InvoiceDelivery? delivery, bool hasEmail, DateTimeOffset now)
    {
        if (delivery?.Status == InvoiceDeliveryStatus.Sent)
        {
            return InvoiceDeliveryState.Sent;
        }

        if (delivery?.IsInFlight(now) == true)
        {
            return InvoiceDeliveryState.Queued;
        }

        if (!hasEmail)
        {
            return InvoiceDeliveryState.NoRecipient;
        }

        return delivery?.Status == InvoiceDeliveryStatus.Failed ? InvoiceDeliveryState.Failed : InvoiceDeliveryState.NotSent;
    }
}
