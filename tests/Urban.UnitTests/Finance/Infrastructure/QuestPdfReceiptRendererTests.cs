using System.Text.RegularExpressions;
using FluentAssertions;
using UglyToad.PdfPig;
using Urban.Finance.Application.Receipts;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.Finance.Infrastructure.Receipts;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Infrastructure;

/// <summary>O PDF do recibo de verdade: o que está escrito nele, lido de volta. Roda sem rede, sem banco e sem fontes do sistema.</summary>
public partial class QuestPdfReceiptRendererTests
{
    private readonly QuestPdfReceiptRenderer _renderer = new();

    /// <summary>Um pagamento parcial de R$ 250,50 numa cobrança de R$ 600,00 que ainda deixa R$ 349,50 por pagar.</summary>
    private static ReceiptDocument Document(
        decimal balance = 349.5m,
        DateOnly? canceledOn = null,
        string? responsible = "Marina Souza",
        InvoiceManager? manager = null,
        bool withManager = true) =>
        new(
            Code: "REC-000123",
            CondominiumName: "Residencial Aurora",
            CondominiumCnpj: "12.345.678/0001-95",
            CondominiumAddress: "Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000",
            UnitLabel: "Bloco A · 102",
            ResponsibleName: responsible,
            Month: YearMonth.Create(2026, 10).Value,
            InvoiceReference: "2026-10-ABCDEF12",
            ChargeTotal: 600m,
            Amount: 250.5m,
            ReceivedOn: new DateOnly(2026, 11, 5),
            Method: PaymentMethod.Transfer,
            PaidSoFar: 600m - balance,
            Balance: balance,
            IssuedOn: new DateOnly(2026, 11, 7),
            CanceledOn: canceledOn,
            Manager: withManager ? manager ?? new InvoiceManager("Ana Maria Silva", "(47) 99123-4567") : null);

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
    public void Render_ShowsWhoIssuesItTheNumberAndWhichUnitAndMonthItIsAbout()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Recibo");
        text.Should().Contain("REC-000123");
        text.Should().Contain("Residencial Aurora");
        text.Should().Contain("12.345.678/0001-95");
        text.Should().Contain("Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000");
        text.Should().Contain("Bloco A · 102");
        text.Should().Contain("Outubro de 2026");
        text.Should().Contain("2026-10-ABCDEF12");
        text.Should().Contain("Marina Souza");
        text.Should().Contain("07/11/2026");
    }

    [Fact]
    public void Render_ShowsWhatWasReceivedAndHow()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Valor recebido").And.Contain("R$ 250,50");
        text.Should().Contain("Data do recebimento").And.Contain("05/11/2026");
        text.Should().Contain("Forma de pagamento").And.Contain("Transferência");
        text.Should().Contain("Valor da fatura").And.Contain("R$ 600,00");
    }

    [Fact]
    public void Render_APartialPayment_ShowsWhatIsStillMissing_AndDoesNotSayItWasSettled()
    {
        string text = Read(_renderer.Render(Document(balance: 349.5m))).Text;

        text.Should().Contain("Total pago até aqui").And.Contain("R$ 250,50");
        text.Should().Contain("Saldo restante").And.Contain("R$ 349,50");
        text.Should().NotContain("quitada");
    }

    [Fact]
    public void Render_ASettledCharge_SaysItWasSettled_WithoutABalanceLine()
    {
        string text = Read(_renderer.Render(Document(balance: 0m))).Text;

        text.Should().Contain("Fatura quitada");
        text.Should().NotContain("Saldo restante");
    }

    [Fact]
    public void Render_ACanceledReceipt_IsStampedWithTheDay_AndLeavesOutTheBalance()
    {
        string text = Read(_renderer.Render(Document(canceledOn: new DateOnly(2026, 11, 8)))).Text;

        text.Should().Contain("CANCELADO");
        text.Should().Contain("08/11/2026");
        text.Should().NotContain("Saldo restante");
        text.Should().NotContain("quitada");
    }

    [Fact]
    public void Render_AValidReceipt_IsNotStampedAsCanceled()
    {
        Read(_renderer.Render(Document())).Text.Should().NotContain("CANCELADO");
    }

    [Fact]
    public void Render_ShowsTheManagerWhoIssuedIt()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().Contain("Síndico").And.Contain("Ana Maria Silva").And.Contain("(47) 99123-4567");
    }

    [Fact]
    public void Render_WithoutResponsibleOrManager_StillProducesAValidPdf_WithoutTheirLabels()
    {
        byte[] pdf = _renderer.Render(Document(responsible: null, withManager: false));

        (string text, int pages) = Read(pdf);
        pages.Should().Be(1);
        text.Should().NotContain("Responsável");
        text.Should().NotContain("Síndico");
    }

    [Fact]
    public void Render_DoesNotMentionAnythingFromTheInvoiceThatDoesNotBelongToAReceipt()
    {
        string text = Read(_renderer.Render(Document())).Text;

        text.Should().NotContain("Como pagar").And.NotContain("Demonstrativo").And.NotContain("Vencimento");
    }
}
