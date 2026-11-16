using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

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
    private const string Success = "#248A3D";
    private const string Warning = "#B25000";
    private const string Danger = "#D70015";

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
            column.Spacing(14);
            column.Item().Element(item => Details(item, document));
            column.Item().Element(item => Charges(item, document));
            column.Item().ShowEntire().Element(item => SituationAndShare(item, document));

            if (document.HowToPay is { } howToPay)
            {
                column.Item().ShowEntire().Element(item => HowToPay(item, document, howToPay));
            }

            if (document.Statement.Count > 0)
            {
                column.Item().Element(item => Statement(item, document));
            }

            if (document.Earlier.Count > 0)
            {
                column.Item().Element(item => Earlier(item, document));
            }
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

                if (document.Manager is { } manager)
                {
                    Field(column, "Contato do síndico", manager.Phone is null ? manager.Name : $"{manager.Name} · {manager.Phone}");
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

    private static void SituationAndShare(IContainer container, InvoiceDocument document) =>
        container.Row(row =>
        {
            row.Spacing(16);
            row.RelativeItem().Column(column =>
            {
                column.Item().Element(item => Section(item, "Situação do pagamento"));
                Pair(column, "Situação", InvoiceText.Status(document.Situation.Status), StatusColor(document.Situation.Status));
                Pair(column, "Pago até agora", InvoiceText.Money(document.Situation.Paid));
                Pair(column, "Saldo a pagar", InvoiceText.Money(document.Situation.Balance));
            });

            row.RelativeItem().Column(column =>
            {
                column.Item().Element(item => Section(item, "Base do rateio"));
                Pair(column, "Método", InvoiceText.Method(document.Share.Method));
                Pair(column, "Participação da unidade", InvoiceText.Percent(document.Share.Percent));
            });
        });

    private static void HowToPay(IContainer container, InvoiceDocument document, InvoiceHowToPay howToPay) =>
        container.Column(column =>
        {
            column.Item().Element(item => Section(item, "Como pagar"));
            column.Item().Row(row =>
            {
                row.Spacing(16);

                if (howToPay.Pix is { } pix)
                {
                    row.RelativeItem().Column(box =>
                    {
                        box.Item().Text("Pix").Bold();
                        box.Item().Text(pix.KindLabel).FontSize(8).FontColor(Muted);
                        box.Item().Text(pix.Key);
                    });
                }

                if (howToPay.Account is { } account)
                {
                    row.RelativeItem().Column(box =>
                    {
                        box.Item().Text("Conta bancária").Bold();
                        box.Item().Text(account.Bank);
                        box.Item().Text($"Agência {account.Agency}");
                        box.Item().Text($"{account.KindLabel} {account.Number}");
                    });
                }
            });
            column.Item().PaddingTop(4).Text($"Favorecido: {document.CondominiumName} · CNPJ {document.CondominiumCnpj}").FontSize(8).FontColor(Muted);
        });

    private static void Statement(IContainer container, InvoiceDocument document) =>
        container.Column(column =>
        {
            column.Item().EnsureSpace(110).Element(item => Section(item, "Demonstrativo do mês", "Quanto o condomínio todo gastou, sem o consumo individual de cada unidade"));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(110);
                });

                foreach (InvoiceStatementLine line in document.Statement)
                {
                    table.Cell().Element(Cell).Text(line.Label);
                    table.Cell().Element(Cell).AlignRight().Text(InvoiceText.Money(line.Amount));
                }
            });
            column.Item().PaddingTop(6).Row(total =>
            {
                total.RelativeItem().Text("Total do condomínio").Bold();
                total.ConstantItem(110).AlignRight().Text(InvoiceText.Money(document.StatementTotal)).Bold();
            });
        });

    private static void Earlier(IContainer container, InvoiceDocument document) =>
        container.Column(column =>
        {
            column.Item().EnsureSpace(110).Element(item => Section(item, "Pendências anteriores", "Cobranças de meses anteriores desta unidade que ainda têm saldo"));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
                });

                table.Header(header =>
                {
                    header.Cell().Element(Heading).Text("Competência");
                    header.Cell().Element(Heading).AlignRight().Text("Vence em");
                    header.Cell().Element(Heading).AlignRight().Text("Valor");
                    header.Cell().Element(Heading).AlignRight().Text("Pago");
                    header.Cell().Element(Heading).AlignRight().Text("Saldo");
                });

                foreach (InvoiceEarlierCharge charge in document.Earlier)
                {
                    table.Cell().Element(Cell).Text(InvoiceText.Competence(charge.Month));
                    table.Cell().Element(Cell).AlignRight().Text(InvoiceText.Date(charge.DueDate));
                    table.Cell().Element(Cell).AlignRight().Text(InvoiceText.Money(charge.Amount));
                    table.Cell().Element(Cell).AlignRight().Text(InvoiceText.Money(charge.Paid));
                    table.Cell().Element(Cell).AlignRight().Text(InvoiceText.Money(charge.Balance)).Bold();
                }
            });
            column.Item().PaddingTop(6).Row(total =>
            {
                total.RelativeItem().Text("Total em aberto").Bold();
                total.ConstantItem(110).AlignRight().Text(InvoiceText.Money(document.EarlierBalance)).Bold().FontColor(Warning);
            });
        });

    private static void Section(IContainer container, string title, string? subtitle = null) =>
        container.PaddingBottom(4).Column(column =>
        {
            column.Item().Text(title).FontSize(11).Bold();

            if (subtitle is not null)
            {
                column.Item().Text(subtitle).FontSize(8).FontColor(Muted);
            }
        });

    private static void Pair(ColumnDescriptor column, string label, string value, string? color = null) =>
        column.Item().PaddingBottom(2).Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Muted);
            TextSpanDescriptor text = row.AutoItem().AlignRight().Text(value);

            if (color is not null)
            {
                text.Bold().FontColor(color);
            }
        });

    private static string StatusColor(ChargeStatus status) => status switch
    {
        ChargeStatus.Paid => Success,
        ChargeStatus.Overdue => Danger,
        ChargeStatus.Partial => Warning,
        _ => Ink,
    };

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
