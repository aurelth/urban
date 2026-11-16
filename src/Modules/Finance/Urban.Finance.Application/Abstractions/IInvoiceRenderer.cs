using Urban.Finance.Application.Invoices;

namespace Urban.Finance.Application.Abstractions;

public interface IInvoiceRenderer
{
    /// <returns>O PDF da fatura. Não depende de rede, de banco nem de fontes do sistema.</returns>
    byte[] Render(InvoiceDocument document);
}
