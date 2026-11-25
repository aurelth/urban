using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Urban.Finance.Infrastructure.Pdf;

/// <summary>
/// O que os PDFs do Financeiro (fatura e recibo) têm em comum: a licença, a fonte, as cores, a página A4, o cabeçalho com os dados
/// do condomínio, o campo de rótulo e valor e o rodapé. A fonte é a Lato que vem dentro do pacote e as fontes do sistema ficam
/// desligadas: a imagem de produção (chiseled) não tem fontconfig nem fontes, e o PDF sai igual em qualquer máquina.
/// </summary>
internal static class PdfStyle
{
    public const string FontFamily = "Lato";
    public const string Ink = "#1C1C1E";
    public const string Muted = "#65657A";
    public const string Accent = "#5856D6";
    public const string Line = "#E6E6EF";

    static PdfStyle()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
    }

    public static byte[] Page(Action<IContainer> header, Action<IContainer> content, Action<IContainer> footer) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontFamily(FontFamily).FontSize(10).FontColor(Ink));
            page.Header().Element(header);
            page.Content().PaddingTop(20).Element(content);
            page.Footer().Element(footer);
        })).GeneratePdf();

    /// <summary>O nome, o CNPJ e o endereço do condomínio à esquerda e, à direita, o título do documento com uma linha abaixo dele.</summary>
    public static void Header(IContainer container, string condominiumName, string cnpj, string address, string title, string subtitle) =>
        container.PaddingBottom(14).BorderBottom(1).BorderColor(Line).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(condominiumName).FontSize(16).Bold();
                column.Item().PaddingTop(2).Text($"CNPJ {cnpj}").FontColor(Muted);
                column.Item().Text(address).FontColor(Muted);
            });

            row.ConstantItem(190).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text(title).FontSize(16).Bold().FontColor(Accent);
                column.Item().AlignRight().PaddingTop(2).Text(subtitle).FontColor(Muted);
            });
        });

    public static void Field(ColumnDescriptor column, string label, string value, bool emphasis = false, bool alignRight = false) =>
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

    public static void Footer(IContainer container, string note) =>
        container.BorderTop(1).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(note).FontSize(8).FontColor(Muted);
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
