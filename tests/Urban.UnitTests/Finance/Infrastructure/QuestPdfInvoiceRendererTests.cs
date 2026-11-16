using System.Text.RegularExpressions;
using FluentAssertions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.Finance.Infrastructure.Invoices;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Infrastructure;

/// <summary>O PDF de verdade: o que está escrito nele, lido de volta, e que ele continua válido com muitas linhas. Roda sem rede, sem banco e sem fontes do sistema.</summary>
public partial class QuestPdfInvoiceRendererTests
{
    private readonly QuestPdfInvoiceRenderer _renderer = new();

    /// <summary>A fatura completa, como o síndico a emite: com tudo o que a 3b acrescentou. É a base dos testes, e cabe numa página.</summary>
    private static InvoiceDocument Document(
        IReadOnlyList<InvoiceLine>? lines = null,
        string? responsible = "Marina Souza",
        string unit = "Bloco A · 102")
    {
        IReadOnlyList<InvoiceLine> used = lines ?? [new InvoiceLine(ChargeLineKind.Expenses, "Despesas ordinárias", 1234.5m, null, null, null)];

        return new InvoiceDocument(
            Reference: "2026-10-ABCDEF12",
            CondominiumName: "Residencial Aurora",
            CondominiumCnpj: "12.345.678/0001-95",
            CondominiumAddress: "Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000",
            UnitLabel: unit,
            ResponsibleName: responsible,
            Month: YearMonth.Create(2026, 10).Value,
            DueDate: new DateOnly(2026, 11, 10),
            IssuedOn: new DateOnly(2026, 11, 7),
            Lines: used,
            Total: used.Sum(line => line.Amount),
            Situation: new InvoiceSituation(ChargeStatus.Partial, 200m, 1034.5m),
            Share: new InvoiceShare(ApportionmentBasisKind.IdealFraction, 2.5m),
            Statement:
            [
                new InvoiceStatementLine("Limpeza", 3000m),
                new InvoiceStatementLine("Portaria", 7000m),
                new InvoiceStatementLine("Fundo de reserva", 700m),
            ],
            Earlier: [new InvoiceEarlierCharge(YearMonth.Create(2026, 9).Value, new DateOnly(2026, 10, 10), 800m, 300m)],
            HowToPay: new InvoiceHowToPay(
                new InvoicePix("CNPJ", "12.345.678/0001-95"),
                new InvoiceAccount("Banco Exemplo", "1234-5", "12345-6", "Conta corrente")),
            Manager: new InvoiceManager("Ana Maria Silva", "(47) 99123-4567"));
    }

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
    public void Render_PushesTheDueDateTheIssueDateAndTheReferenceToTheRightEdge()
    {
        using PdfDocument pdf = PdfDocument.Open(_renderer.Render(Document()));
        Page page = pdf.GetPage(1);
        const double marginInPoints = 36;
        double rightEdge = page.Width - marginInPoints;
        Word[] words = [.. page.GetWords()];

        foreach (string text in new[] { "10/11/2026", "07/11/2026", "2026-10-ABCDEF12", "Vencimento", "Referência" })
        {
            Word word = words.Single(candidate => candidate.Text == text);

            word.BoundingBox.Right.Should().BeApproximately(rightEdge, 2, $"\"{text}\" fica rente à margem direita, como a coluna Valor");
        }

        words.Single(candidate => candidate.Text == "Responsável").BoundingBox.Left.Should().BeApproximately(marginInPoints, 2, "o bloco da unidade continua à esquerda");
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

    // ---- Situação do pagamento ----

    [Theory]
    [InlineData(ChargeStatus.Open, "Em aberto")]
    [InlineData(ChargeStatus.Partial, "Paga em parte")]
    [InlineData(ChargeStatus.Paid, "Paga")]
    [InlineData(ChargeStatus.Overdue, "Vencida")]
    public void Render_ShowsTheSituationOfThePayment(ChargeStatus status, string label)
    {
        InvoiceDocument document = Document() with { Situation = new InvoiceSituation(status, 0m, 1234.5m) };

        string text = Read(_renderer.Render(document)).Text;

        text.Should().Contain("Situação do pagamento").And.Contain(label);
    }

    [Fact]
    public void Render_ShowsWhatWasPaidAndWhatIsLeft()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Pago até agora R$ 200,00");
        text.Should().Contain("Saldo a pagar R$ 1.034,50");
    }

    // ---- Base do rateio ----

    [Fact]
    public void Render_ExplainsHowTheValueWasDivided()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Base do rateio");
        text.Should().Contain("Fração ideal").And.Contain("2,5%");
    }

    // ---- Demonstrativo ----

    [Fact]
    public void Render_ShowsTheStatementOfTheMonthWithItsTotal()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Demonstrativo do mês");
        text.Should().Contain("Limpeza R$ 3.000,00");
        text.Should().Contain("Portaria R$ 7.000,00");
        text.Should().Contain("Fundo de reserva R$ 700,00");
        text.Should().Contain("Total do condomínio R$ 10.700,00");
    }

    [Fact]
    public void Render_WithoutStatementLines_LeavesTheSectionOut()
    {
        string text = Read(_renderer.Render(Document() with { Statement = [] })).Text;

        text.Should().NotContain("Demonstrativo do mês");
    }

    // ---- Pendências anteriores ----

    [Fact]
    public void Render_ListsTheEarlierChargesStillOwed()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Pendências anteriores");
        text.Should().Contain("Setembro de 2026").And.Contain("10/10/2026");
        text.Should().Contain("R$ 800,00").And.Contain("R$ 300,00").And.Contain("R$ 500,00");
        text.Should().Contain("Total em aberto R$ 500,00");
    }

    [Fact]
    public void Render_WithoutEarlierCharges_LeavesTheSectionOut()
    {
        string text = Read(_renderer.Render(Document() with { Earlier = [] })).Text;

        text.Should().NotContain("Pendências anteriores");
    }

    [Fact]
    public void Render_WithManyEarlierCharges_FlowsToMorePagesAndKeepsTheTotal()
    {
        InvoiceEarlierCharge[] earlier =
        [
            .. Enumerable.Range(0, 36).Select(n => new InvoiceEarlierCharge(YearMonth.Create(2023, 10).Value.AddMonths(n), new DateOnly(2023, 11, 10).AddMonths(n), 100m, 0m)),
        ];

        (string text, int pages) = Read(_renderer.Render(Document() with { Earlier = earlier }));

        pages.Should().BeGreaterThan(1);
        text.Should().Contain("Outubro de 2023").And.Contain("Setembro de 2026");
        text.Should().Contain("Total em aberto R$ 3.600,00");
    }

    // ---- Como pagar ----

    [Fact]
    public void Render_ShowsThePixKeyAndTheAccount()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Como pagar");
        text.Should().Contain("Pix").And.Contain("CNPJ").And.Contain("12.345.678/0001-95");
        text.Should().Contain("Banco Exemplo").And.Contain("Agência 1234-5").And.Contain("Conta corrente 12345-6");
    }

    [Fact]
    public void Render_WithOnlyPix_DoesNotInventAnAccount()
    {
        string text = Read(_renderer.Render(Document() with { HowToPay = new InvoiceHowToPay(new InvoicePix("E-mail", "financeiro@aurora.com.br"), null) })).Text;

        text.Should().Contain("financeiro@aurora.com.br");
        text.Should().NotContain("Agência");
    }

    [Fact]
    public void Render_WithOnlyTheAccount_DoesNotInventAPixKey()
    {
        string text = Read(_renderer.Render(Document() with { HowToPay = new InvoiceHowToPay(null, new InvoiceAccount("Banco Exemplo", "1234", "12345", "Conta poupança")) })).Text;

        text.Should().Contain("Conta poupança 12345").And.Contain("Agência 1234");
        text.Should().NotContain("Pix");
    }

    [Fact]
    public void Render_WithoutPaymentData_LeavesTheSectionOut()
    {
        string text = Read(_renderer.Render(Document() with { HowToPay = null })).Text;

        text.Should().NotContain("Como pagar");
    }

    // ---- Contato do síndico ----

    [Fact]
    public void Render_ShowsTheManagerContact()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Contato do síndico");
        text.Should().Contain("Ana Maria Silva").And.Contain("(47) 99123-4567");
    }

    [Fact]
    public void Render_ManagerWithoutPhone_ShowsOnlyTheName()
    {
        string text = Read(_renderer.Render(Document() with { Manager = new InvoiceManager("Ana Maria Silva", null) })).Text;

        text.Should().Contain("Contato do síndico").And.Contain("Ana Maria Silva");
        text.Should().NotContain("(47)");
    }

    [Fact]
    public void Render_WithoutAManager_LeavesTheContactOut()
    {
        string text = Read(_renderer.Render(Document() with { Manager = null })).Text;

        text.Should().NotContain("Contato do síndico");
    }

    [Fact]
    public void Render_TheMostCompleteInvoiceStillFitsOnePage()
    {
        Read(_renderer.Render(Document())).Pages.Should().Be(1);
    }
}
