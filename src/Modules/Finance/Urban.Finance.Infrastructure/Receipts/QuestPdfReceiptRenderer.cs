using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Application.Receipts;
using static Urban.Finance.Infrastructure.Pdf.PdfStyle;

namespace Urban.Finance.Infrastructure.Receipts;

/// <summary>
/// Desenha o recibo em PDF com o QuestPDF, no mesmo estilo da fatura (a página, as cores e a fonte são de <see cref="Pdf.PdfStyle"/>).
/// O recibo cabe numa página e só fala do que foi recebido: nada de vencimento, demonstrativo nem como pagar, que são da fatura.
/// </summary>
public sealed class QuestPdfReceiptRenderer : IReceiptRenderer
{
    private const string Danger = "#B3261E";
    private const string Success = "#248A3D";

    public byte[] Render(ReceiptDocument document) =>
        Page(
            header => Header(header, document.CondominiumName, document.CondominiumCnpj, document.CondominiumAddress, "Recibo", document.Code),
            content => Body(content, document),
            footer => Footer(footer, "Gerado pelo Urban. Este recibo comprova o recebimento do valor acima."));

    private static void Body(IContainer container, ReceiptDocument document) =>
        container.Column(column =>
        {
            column.Spacing(14);

            if (document.CanceledOn is { } canceledOn)
            {
                column.Item().Element(item => CanceledNotice(item, canceledOn));
            }

            column.Item().Element(item => Details(item, document));
            column.Item().Element(item => Declaration(item, document));
            column.Item().Element(item => Amounts(item, document));
        });

    private static void CanceledNotice(IContainer container, DateOnly canceledOn) =>
        container.Border(1).BorderColor(Danger).Padding(10).Column(column =>
        {
            column.Item().Text($"CANCELADO em {InvoiceText.Date(canceledOn)}").FontSize(14).Bold().FontColor(Danger);
            column.Item().PaddingTop(2).Text("O pagamento foi estornado, então este recibo não vale como comprovante.").FontColor(Danger);
        });

    private static void Details(IContainer container, ReceiptDocument document) =>
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                Field(column, "Unidade", document.UnitLabel);
                Field(column, "Competência", InvoiceText.Competence(document.Month));

                if (document.ResponsibleName is not null)
                {
                    Field(column, "Responsável", document.ResponsibleName);
                }

                if (document.Manager is { } manager)
                {
                    Field(column, "Síndico", manager.Phone is null ? manager.Name : $"{manager.Name} · {manager.Phone}");
                }
            });

            row.ConstantItem(190).Column(column =>
            {
                Field(column, "Recibo", document.Code, emphasis: true, alignRight: true);
                Field(column, "Emitido em", InvoiceText.Date(document.IssuedOn), alignRight: true);
                Field(column, "Fatura", document.InvoiceReference, alignRight: true);
            });
        });

    private static void Declaration(IContainer container, ReceiptDocument document)
    {
        string from = document.ResponsibleName is { } name ? name : $"a unidade {document.UnitLabel}";

        container.Text(
            $"Recebemos de {from} a quantia de {InvoiceText.Money(document.Amount)}, em {InvoiceText.Date(document.ReceivedOn)}, "
            + $"por {ReceiptText.Method(document.Method)}, referente à fatura de {InvoiceText.Competence(document.Month)} da unidade {document.UnitLabel}.");
    }

    private static void Amounts(IContainer container, ReceiptDocument document) =>
        container.Column(column =>
        {
            column.Item().Element(Row("Valor recebido", InvoiceText.Money(document.Amount), strong: true));
            column.Item().Element(Row("Data do recebimento", InvoiceText.Date(document.ReceivedOn)));
            column.Item().Element(Row("Forma de pagamento", ReceiptText.Method(document.Method)));
            column.Item().Element(Row("Valor da fatura", InvoiceText.Money(document.ChargeTotal)));

            if (document.CanceledOn is not null)
            {
                return;
            }

            column.Item().Element(Row("Total pago até aqui", InvoiceText.Money(document.PaidSoFar)));

            column.Item().PaddingTop(6).Element(document.Balance > 0
                ? Row("Saldo restante", InvoiceText.Money(document.Balance), strong: true)
                : Row("Fatura quitada", InvoiceText.Money(document.ChargeTotal), strong: true, tint: Success));
        });

    private static Action<IContainer> Row(string label, string value, bool strong = false, string? tint = null) =>
        container => container.BorderBottom(1).BorderColor(Line).PaddingVertical(6).Row(row =>
        {
            TextSpanDescriptor labelText = row.RelativeItem().Text(label);
            TextSpanDescriptor valueText = row.ConstantItem(160).AlignRight().Text(value);

            if (strong)
            {
                labelText.Bold();
                valueText.Bold();
            }

            if (tint is not null)
            {
                labelText.FontColor(tint);
                valueText.FontColor(tint);
            }
        });
}
