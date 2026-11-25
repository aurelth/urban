using FluentAssertions;
using Urban.Finance.Application.Receipts;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

public class ReceiptEmailTests
{
    private static readonly InvoiceResponsible Responsible = new("Maria Souza", "maria@exemplo.com");

    private static Payment Paid(decimal amount = 1234.5m, PaymentMethod method = PaymentMethod.Pix)
    {
        Payment payment = Payment.Register(
            Guid.CreateVersion7(), Guid.CreateVersion7(), FinanceTestKit.M(2026, 10), amount, new DateOnly(2026, 11, 5), method, null, Guid.CreateVersion7(),
            FinanceTestKit.Now, new DateOnly(2026, 11, 7)).Value;
        payment.IssueReceipt(123);

        return payment;
    }

    private static InvoiceCondominium Condominium(InvoiceManager? manager) => new("Residencial Aurora", "12.345.678/0001-95", "Rua das Flores, 100", manager);

    private static string Body(Payment payment, InvoiceManager? manager) =>
        ReceiptEmail.Body(payment, "Bloco A · 102", FinanceTestKit.M(2026, 10), Condominium(manager), Responsible);

    [Fact]
    public void TheSubjectNamesTheReceiptAndTheCondominium()
    {
        ReceiptEmail.Subject(Paid(), Condominium(null)).Should().Be("Recibo REC-000123 - Residencial Aurora");
    }

    [Fact]
    public void TheBodyGreetsTheResponsibleAndSaysWhatIsAttachedAndWhatWasReceived()
    {
        string body = Body(Paid(), new InvoiceManager("Ana Maria Silva", "(47) 99123-4567"));

        body.Should().Contain("Olá, Maria Souza.");
        body.Should().Contain("Segue em anexo o recibo REC-000123 do pagamento da fatura de Outubro de 2026 da unidade Bloco A · 102, do condomínio Residencial Aurora.");
        body.Should().Contain("Valor recebido: R$ 1.234,50");
        body.Should().Contain("Data do recebimento: 05/11/2026");
        body.Should().Contain("Forma de pagamento: Pix");
        body.Should().Contain("Esta mensagem é automática: não responda a este e-mail.");
        body.TrimEnd().Should().EndWith("Urban");
    }

    [Theory]
    [InlineData(PaymentMethod.Boleto, "Boleto")]
    [InlineData(PaymentMethod.Transfer, "Transferência")]
    [InlineData(PaymentMethod.Cash, "Dinheiro")]
    public void TheBodyNamesTheMethodInPortuguese(PaymentMethod method, string text)
    {
        Body(Paid(100m, method), null).Should().Contain($"Forma de pagamento: {text}");
    }

    [Fact]
    public void TheBodyNeverCarriesTheInternalNoteOfThePayment()
    {
        Payment payment = Payment.Register(
            Guid.CreateVersion7(), Guid.CreateVersion7(), FinanceTestKit.M(2026, 10), 100m, new DateOnly(2026, 11, 5), PaymentMethod.Pix, "Anotação do síndico",
            Guid.CreateVersion7(), FinanceTestKit.Now, new DateOnly(2026, 11, 7)).Value;
        payment.IssueReceipt(1);

        Body(payment, null).Should().NotContain("Anotação do síndico");
    }

    [Fact]
    public void TheBodyPointsToTheManagerWithThePhoneWhenThereIsOne()
    {
        Body(Paid(), new InvoiceManager("Ana Maria Silva", "(47) 99123-4567"))
            .Should().Contain("Dúvidas sobre o recibo? Fale com o síndico, Ana Maria Silva, pelo telefone (47) 99123-4567.");
    }

    [Fact]
    public void TheBodyPointsToTheManagerWithoutThePhoneWhenThereIsNone()
    {
        Body(Paid(), new InvoiceManager("Ana Maria Silva", null)).Should().Contain("Dúvidas sobre o recibo? Fale com o síndico, Ana Maria Silva.");
    }

    [Fact]
    public void TheBodyStillPointsToTheManagerWhenTheirRecordWasNotFound()
    {
        Body(Paid(), null).Should().Contain("Dúvidas sobre o recibo? Fale com o síndico do condomínio.");
    }
}
