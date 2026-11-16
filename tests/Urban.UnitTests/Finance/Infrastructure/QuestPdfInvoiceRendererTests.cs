using System.Text.RegularExpressions;
using FluentAssertions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Invoices;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Infrastructure;

/// <summary>O PDF de verdade: o que está escrito nele, lido de volta, e que ele continua válido com muitas linhas. Roda sem rede, sem banco e sem fontes do sistema.</summary>
public partial class QuestPdfInvoiceRendererTests
{
    private readonly QuestPdfInvoiceRenderer _renderer = new();

    /// <summary>A fatura completa, como o síndico a emite: o pedido de pagamento, sem nada de pagamento feito. É a base dos testes, e cabe numa página.</summary>
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
            Share: new InvoiceShare(ApportionmentBasisKind.IdealFraction, 2.5m),
            Statement: new InvoiceStatement(
                [
                    new InvoiceStatementGroup("Limpeza", [new InvoiceStatementItem("Faxineira", 2000m), new InvoiceStatementItem("Produtos de limpeza", 1000m)]),
                    new InvoiceStatementGroup("Portaria", [new InvoiceStatementItem("Porteiro diurno", 4000m), new InvoiceStatementItem("Porteiro noturno", 3000m)]),
                ],
                new InvoiceReserveFund(700m, 7m, 10000m)),
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

    // ---- Fatura não é recibo ----

    [Fact]
    public void Render_ShowsNothingAboutPaymentsAlreadyMade()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().NotContain("Situação do pagamento");
        text.Should().NotContain("Pago até agora");
        text.Should().NotContain("Saldo a pagar");
        text.Should().NotContain("Pendências anteriores");
    }

    // ---- Participação no rateio ----

    [Fact]
    public void Render_ShowsTheShareOfTheUnitAndTheMethodNextToTheUnit()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Participação no rateio");
        text.Should().Contain("2,5% · Fração ideal");
    }

    // ---- Demonstrativo ----

    [Fact]
    public void Render_ListsEveryExpenseOfTheMonthByGroupBeforeTheTotal()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Demonstrativo do mês");
        text.Should().Contain("Limpeza");
        text.Should().Contain("Faxineira R$ 2.000,00");
        text.Should().Contain("Produtos de limpeza R$ 1.000,00");
        text.Should().Contain("Subtotal de Limpeza R$ 3.000,00");
        text.Should().Contain("Porteiro diurno R$ 4.000,00");
        text.Should().Contain("Porteiro noturno R$ 3.000,00");
        text.Should().Contain("Subtotal de Portaria R$ 7.000,00");
        text.Should().Contain("Fundo de reserva").And.Contain("R$ 700,00");
        text.Should().Contain("Total do condomínio R$ 10.700,00");
        text.IndexOf("Porteiro noturno", StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("Total do condomínio", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_TheReserveFundSaysThePercentAndTheBaseItWasAppliedTo()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("7% sobre R$ 10.000,00 de despesas");
    }

    [Fact]
    public void Render_TheReserveFundLineOfTheUnitExplainsItToo()
    {
        InvoiceLine[] lines =
        [
            new(ChargeLineKind.Expenses, "Despesas ordinárias", 600m, null, null, null),
            new(ChargeLineKind.ReserveFund, "Fundo de reserva", 42m, null, null, null),
        ];

        string text = Read(_renderer.Render(Document(lines))).Text;

        Regex.Matches(text, @"7% sobre R\$ 10\.000,00 de despesas").Count.Should().Be(2, "uma vez na linha da unidade e outra no demonstrativo");
    }

    [Fact]
    public void Render_AClosingFromBeforeThePercentWasFrozen_ShowsOnlyTheAmount()
    {
        InvoiceDocument document = Document() with { Statement = Document().Statement with { ReserveFund = new InvoiceReserveFund(700m, null, null) } };

        string text = Read(_renderer.Render(document)).Text;

        text.Should().Contain("Fundo de reserva").And.Contain("R$ 700,00");
        text.Should().NotContain("sobre R$");
    }

    [Fact]
    public void Render_WithoutReserveFund_LeavesTheLineOut()
    {
        InvoiceDocument document = Document() with { Statement = Document().Statement with { ReserveFund = null } };

        string text = Read(_renderer.Render(document)).Text;

        text.Should().NotContain("Fundo de reserva");
        text.Should().Contain("Total do condomínio R$ 10.000,00");
    }

    [Fact]
    public void Render_WithoutExpenses_LeavesTheSectionOut()
    {
        string text = Read(_renderer.Render(Document() with { Statement = new InvoiceStatement([], null) })).Text;

        text.Should().NotContain("Demonstrativo do mês");
    }

    [Fact]
    public void Render_WithManyExpenses_FlowsToMorePagesAndKeepsTheTotalAtTheEnd()
    {
        InvoiceStatementGroup[] groups =
        [
            .. Enumerable.Range(1, 3).Select(g => new InvoiceStatementGroup(
                $"Grupo {g}",
                [.. Enumerable.Range(1, 25).Select(n => new InvoiceStatementItem($"Despesa {g}-{n}", 10m))])),
        ];

        (string text, int pages) = Read(_renderer.Render(Document() with { Statement = new InvoiceStatement(groups, null) }));

        pages.Should().BeGreaterThan(1);
        text.Should().Contain("Despesa 1-1 ").And.Contain("Despesa 3-25");
        text.Should().Contain("Subtotal de Grupo 3 R$ 250,00");
        text.Should().Contain("Total do condomínio R$ 750,00");
    }

    [Fact]
    public void Render_AGroupWithMoreItemsThanAPage_DoesNotThrow()
    {
        InvoiceStatementGroup huge = new("Manutenção", [.. Enumerable.Range(1, 120).Select(n => new InvoiceStatementItem($"Serviço número {n}", 5m))]);

        Action render = () => _renderer.Render(Document() with { Statement = new InvoiceStatement([huge], null) });

        render.Should().NotThrow();
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
