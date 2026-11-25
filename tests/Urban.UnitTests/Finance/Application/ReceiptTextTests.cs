using FluentAssertions;
using Urban.Finance.Application.Receipts;
using Urban.Finance.Domain.Payments;

namespace Urban.UnitTests.Finance.Application;

/// <summary>Os textos do recibo, escritos à mão para o PDF sair igual em qualquer máquina.</summary>
public class ReceiptTextTests
{
    [Theory]
    [InlineData(1, "REC-000001")]
    [InlineData(123, "REC-000123")]
    [InlineData(999999, "REC-999999")]
    [InlineData(1234567, "REC-1234567")]
    public void Code_HasAtLeastSixDigits_AndGrowsWithoutCutting(int number, string expected)
    {
        ReceiptText.Code(number).Should().Be(expected);
    }

    [Fact]
    public void FileName_IsSafeAndCarriesTheNumber()
    {
        ReceiptText.FileName(123).Should().Be("recibo-000123.pdf");
        ReceiptText.FileName(1234567).Should().Be("recibo-1234567.pdf");
    }

    [Theory]
    [InlineData(PaymentMethod.Pix, "Pix")]
    [InlineData(PaymentMethod.Boleto, "Boleto")]
    [InlineData(PaymentMethod.Transfer, "Transferência")]
    [InlineData(PaymentMethod.Cash, "Dinheiro")]
    [InlineData(PaymentMethod.Other, "Outra forma")]
    public void Method_SaysHowTheMoneyArrived(PaymentMethod method, string expected)
    {
        ReceiptText.Method(method).Should().Be(expected);
    }
}
