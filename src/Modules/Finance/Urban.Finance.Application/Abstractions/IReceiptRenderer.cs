using Urban.Finance.Application.Receipts;

namespace Urban.Finance.Application.Abstractions;

public interface IReceiptRenderer
{
    /// <returns>O PDF do recibo. Não depende de rede, de banco nem de fontes do sistema.</returns>
    byte[] Render(ReceiptDocument document);
}
