using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Receipts;

namespace Urban.Finance.Infrastructure.Receipts;

/// <summary>Desenha o recibo em PDF com o QuestPDF, no mesmo estilo da fatura.</summary>
public sealed class QuestPdfReceiptRenderer : IReceiptRenderer
{
    public byte[] Render(ReceiptDocument document) => throw new NotImplementedException();
}
