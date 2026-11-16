using System.Text.RegularExpressions;
using FluentAssertions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Invoices;

namespace Urban.UnitTests.Finance.Infrastructure;

/// <summary>O PDF de verdade: o que está escrito nele, lido de volta, e que ele continua válido com muitas linhas. Roda sem rede, sem banco e sem fontes do sistema.</summary>
public partial class QuestPdfInvoiceRendererTests
{
    private readonly QuestPdfInvoiceRenderer _renderer = new();

    private static InvoiceDocument Document(
        IReadOnlyList<InvoiceLine>? lines = null,
        string? responsible = "Marina Souza",
        string unit = "Bloco A · 102") =>
        new(
            Reference: "2026-10-ABCDEF12",
            CondominiumName: "Residencial Aurora",
            CondominiumCnpj: "12.345.678/0001-95",
            CondominiumAddress: "Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000",
            UnitLabel: unit,
            ResponsibleName: responsible,
            Month: YearMonth.Create(2026, 10).Value,
            DueDate: new DateOnly(2026, 11, 10),
            IssuedOn: new DateOnly(2026, 11, 7),
            Lines: lines ?? [new InvoiceLine(ChargeLineKind.Expenses, "Despesas ordinárias", 1234.5m, null, null, null)],
            Total: (lines ?? [new InvoiceLine(ChargeLineKind.Expenses, "Despesas ordinárias", 1234.5m, null, null, null)]).Sum(line => line.Amount));

    private static (string Text, int Pages) Read(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        string text = string.Join(' ', document.GetPages().SelectMany(page => page.GetWords().Select(word => word.Text)));

        return (Spaces().Replace(text, " "), document.NumberOfPages);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [Fact]
    public void Render_ProducesAValidSinglePagePdf()
    {
        byte[] pdf = _renderer.Render(Document());

        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        Read(pdf).Pages.Should().Be(1);
    }

    [Fact]
    public void Render_ShowsWhoIssuesItAndWhichUnitAndMonthItBills()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Residencial Aurora");
        text.Should().Contain("12.345.678/0001-95");
        text.Should().Contain("Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000");
        text.Should().Contain("Bloco A · 102");
        text.Should().Contain("Outubro de 2026");
        text.Should().Contain("2026-10-ABCDEF12");
    }

    [Fact]
    public void Render_ShowsTheDueDateTheIssueDateAndTheTotal()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("10/11/2026");
        text.Should().Contain("07/11/2026");
        text.Should().Contain("R$ 1.234,50");
    }

    [Fact]
    public void Render_ShowsEachLineWithItsAmount_AndKeepsTheAccents()
    {
        InvoiceLine[] lines =
        [
            new(ChargeLineKind.Expenses, "Despesas ordinárias", 600m, null, null, null),
            new(ChargeLineKind.ReserveFund, "Fundo de reserva", 60m, null, null, null),
            new(ChargeLineKind.Sewage, "Esgoto", 12.34m, null, null, null),
        ];

        string text = Read(_renderer.Render(Document(lines))).Text;

        text.Should().Contain("Despesas ordinárias").And.Contain("R$ 600,00");
        text.Should().Contain("Fundo de reserva").And.Contain("R$ 60,00");
        text.Should().Contain("Esgoto").And.Contain("R$ 12,34");
        text.Should().Contain("R$ 672,34");
    }

    [Fact]
    public void Render_ShowsTheConsumptionAndTheReadingsOfWaterAndGas()
    {
        InvoiceLine[] lines =
        [
            new(ChargeLineKind.Water, "Água", 85.4m, 12.5m, 100m, 112.5m),
            new(ChargeLineKind.Gas, "Gás", 40m, 2.25m, 10m, 12.25m),
        ];

        string text = Read(_renderer.Render(Document(lines))).Text;

        text.Should().Contain("12,5 m³").And.Contain("100").And.Contain("112,5");
        text.Should().Contain("2,25 m³").And.Contain("12,25");
    }

    [Fact]
    public void Render_NamesTheResponsible_WhenThereIsOne()
    {
        Read(_renderer.Render(Document(responsible: "Marina Souza"))).Text.Should().Contain("Marina Souza");
    }

    [Fact]
    public void Render_WithoutAResponsible_LeavesTheLineOut()
    {
        Read(_renderer.Render(Document(responsible: null))).Text.Should().NotContain("Responsável");
    }

    [Fact]
    public void Render_WithManyLines_FlowsToMorePagesAndKeepsTheTotalAtTheEnd()
    {
        InvoiceLine[] lines = [.. Enumerable.Range(1, 90).Select(n => new InvoiceLine(ChargeLineKind.Expenses, $"Grupo de despesas número {n}", 10m, null, null, null))];

        (string text, int pages) = Read(_renderer.Render(Document(lines)));

        pages.Should().BeGreaterThan(1);
        text.Should().Contain("Grupo de despesas número 1 ").And.Contain("Grupo de despesas número 90");
        text.Should().Contain("R$ 900,00");
    }

    [Fact]
    public void Render_WithVeryLongTexts_DoesNotThrow()
    {
        string longName = string.Join(' ', Enumerable.Repeat("Associação dos Moradores do Residencial", 12));
        InvoiceDocument document = Document(responsible: longName) with { CondominiumName = longName };

        Action render = () => _renderer.Render(document);

        render.Should().NotThrow();
    }
}
