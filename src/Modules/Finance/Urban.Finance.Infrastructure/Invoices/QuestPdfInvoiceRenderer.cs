using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Closing;

namespace Urban.Finance.Infrastructure.Invoices;

/// <summary>
/// Desenha a fatura em PDF com o QuestPDF. A fonte é a Lato que vem dentro do pacote e as fontes do sistema ficam desligadas: a
/// imagem de produção (chiseled) não tem fontconfig nem fontes, e o PDF sai igual em qualquer máquina.
/// </summary>
public sealed class QuestPdfInvoiceRenderer : IInvoiceRenderer
{
    private const string FontFamily = "Lato";
    private const string Ink = "#1C1C1E";
    private const string Muted = "#65657A";
    private const string Accent = "#5856D6";
    private const string Line = "#E6E6EF";

    static QuestPdfInvoiceRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
    }

    public byte[] Render(InvoiceDocument document) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Ink));
            page.Header().Element(header => Header(header, document));
            page.Content().PaddingTop(20).Element(content => Body(content, document));
            page.Footer().Element(Footer);
        })).GeneratePdf();

    private static void Header(IContainer container, InvoiceDocument document) =>
        container.PaddingBottom(14).BorderBottom(1).BorderColor(Line).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(document.CondominiumName).FontSize(16).Bold();
                column.Item().PaddingTop(2).Text($"CNPJ {document.CondominiumCnpj}").FontColor(Muted);
                column.Item().Text(document.CondominiumAddress).FontColor(Muted);
            });

            row.ConstantItem(190).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text("Fatura").FontSize(16).Bold().FontColor(Accent);
                column.Item().AlignRight().PaddingTop(2).Text(InvoiceText.Competence(document.Month)).FontColor(Muted);
            });
        });

    private static void Body(IContainer container, InvoiceDocument document) =>
        container.Column(column =>
        {
            column.Spacing(18);
            column.Item().Element(item => Details(item, document));
            column.Item().Element(item => Charges(item, document));
        });

    private static void Details(IContainer container, InvoiceDocument document) =>
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                Field(column, "Unidade", document.UnitLabel);

                if (document.ResponsibleName is not null)
                {
                    Field(column, "Responsável", document.ResponsibleName);
                }
            });

            row.ConstantItem(190).Column(column =>
            {
                Field(column, "Vencimento", InvoiceText.Date(document.DueDate), emphasis: true, alignRight: true);
                Field(column, "Emitida em", InvoiceText.Date(document.IssuedOn), alignRight: true);
                Field(column, "Referência", document.Reference, alignRight: true);
            });
        });

    private static void Field(ColumnDescriptor column, string label, string value, bool emphasis = false, bool alignRight = false) =>
        column.Item().PaddingBottom(6).Column(field =>
        {
            IContainer labelBox = field.Item();
            IContainer valueBox = field.Item();

            if (alignRight)
            {
                labelBox = labelBox.AlignRight();
                valueBox = valueBox.AlignRight();
            }

            labelBox.Text(label).FontSize(8).FontColor(Muted);
            TextSpanDescriptor text = valueBox.Text(value);

            if (emphasis)
            {
                text.Bold().FontColor(Accent);
            }
        });

    private static void Charges(IContainer container, InvoiceDocument document) =>
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(110);
                });

                table.Header(header =>
                {
                    header.Cell().Element(Heading).Text("Descrição");
                    header.Cell().Element(Heading).AlignRight().Text("Valor");
                });

                foreach (InvoiceLine line in document.Lines)
                {
                    table.Cell().Element(Cell).Column(label =>
                    {
                        label.Item().Text(line.Label);

                        if (Detail(line) is { } detail)
                        {
                            label.Item().Text(detail).FontSize(8).FontColor(Muted);
                        }
                    });

                    table.Cell().Element(Cell).AlignRight().Text(InvoiceText.Money(line.Amount));
                }
            });

            column.Item().PaddingTop(10).Row(total =>
            {
                total.RelativeItem().Text("Total da fatura").FontSize(12).Bold();
                total.ConstantItem(110).AlignRight().Text(InvoiceText.Money(document.Total)).FontSize(12).Bold().FontColor(Accent);
            });
        });

    private static string? Detail(InvoiceLine line) =>
        line.Kind is ChargeLineKind.Water or ChargeLineKind.Sewage or ChargeLineKind.Gas && line.Quantity is { } quantity
            ? line.PreviousReading is { } previous && line.CurrentReading is { } current
                ? $"Consumo de {InvoiceText.Cubic(quantity)} m³ (leitura de {InvoiceText.Cubic(previous)} a {InvoiceText.Cubic(current)})"
                : $"Consumo de {InvoiceText.Cubic(quantity)} m³"
            : null;

    private static IContainer Heading(IContainer container) =>
        container.BorderBottom(1).BorderColor(Ink).PaddingBottom(4).DefaultTextStyle(style => style.FontSize(8).FontColor(Muted).Bold());

    private static IContainer Cell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Line).PaddingVertical(6);

    private static void Footer(IContainer container) =>
        container.BorderTop(1).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("Gerada pelo Urban. Confira o valor e o vencimento com o síndico.").FontSize(8).FontColor(Muted);
            row.ConstantItem(80).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.Span("Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });
}
