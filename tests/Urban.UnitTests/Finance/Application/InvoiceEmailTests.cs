using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Invoices;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

public class InvoiceEmailTests
{
    private static readonly ClosingCharge Charge = new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), "A", "102", FinanceTestKit.M(2026, 10), new DateOnly(2026, 11, 10), 1234.5m);

    private static readonly InvoiceResponsible Responsible = new("Maria Souza", "maria@exemplo.com");

    private static InvoiceCondominium Condominium(InvoiceManager? manager) => new("Residencial Aurora", "12.345.678/0001-95", "Rua das Flores, 100", manager);

    [Fact]
    public void TheSubjectNamesTheMonthAndTheCondominium()
    {
        InvoiceEmail.Subject(Charge, Condominium(null)).Should().Be("Fatura de Outubro de 2026 - Residencial Aurora");
    }

    [Fact]
    public void TheBodyGreetsTheResponsibleAndSaysWhatIsAttachedHowMuchAndUntilWhen()
    {
        string body = InvoiceEmail.Body(Charge, Condominium(new InvoiceManager("Ana Maria Silva", "(47) 99123-4567")), Responsible);

        body.Should().Contain("Olá, Maria Souza.");
        body.Should().Contain("Segue em anexo a fatura de Outubro de 2026 da unidade Bloco A · 102, do condomínio Residencial Aurora.");
        body.Should().Contain("Valor: R$ 1.234,50");
        body.Should().Contain("Vencimento: 10/11/2026");
        body.Should().Contain("Esta mensagem é automática: não responda a este e-mail.");
        body.TrimEnd().Should().EndWith("Urban");
    }

    [Fact]
    public void TheBodyPointsToTheManagerWithThePhoneWhenThereIsOne()
    {
        InvoiceEmail.Body(Charge, Condominium(new InvoiceManager("Ana Maria Silva", "(47) 99123-4567")), Responsible)
            .Should().Contain("Dúvidas sobre a fatura? Fale com o síndico, Ana Maria Silva, pelo telefone (47) 99123-4567.");
    }

    [Fact]
    public void TheBodyPointsToTheManagerWithoutThePhoneWhenThereIsNone()
    {
        InvoiceEmail.Body(Charge, Condominium(new InvoiceManager("Ana Maria Silva", null)), Responsible)
            .Should().Contain("Dúvidas sobre a fatura? Fale com o síndico, Ana Maria Silva.");
        InvoiceEmail.Body(Charge, Condominium(new InvoiceManager("Ana Maria Silva", "")), Responsible)
            .Should().Contain("Dúvidas sobre a fatura? Fale com o síndico, Ana Maria Silva.");
    }

    [Fact]
    public void TheBodyStillPointsToTheManagerWhenTheirRecordWasNotFound()
    {
        InvoiceEmail.Body(Charge, Condominium(null), Responsible).Should().Contain("Dúvidas sobre a fatura? Fale com o síndico do condomínio.");
    }

    [Fact]
    public void ForAUnitWithoutABlock_TheBodyNamesJustTheNumber()
    {
        ClosingCharge house = Charge with { Block = null, Number = "12" };

        InvoiceEmail.Body(house, Condominium(null), Responsible).Should().Contain("da unidade 12, do condomínio");
    }
}
