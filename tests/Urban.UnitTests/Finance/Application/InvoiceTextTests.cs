using FluentAssertions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>Os textos da fatura em português do Brasil, sem depender da cultura do servidor (a imagem de produção pode estar com globalização invariante).</summary>
public class InvoiceTextTests
{
    public static TheoryData<decimal, string> Amounts => new()
    {
        { 0m, "R$ 0,00" },
        { 600m, "R$ 600,00" },
        { 1234.5m, "R$ 1.234,50" },
        { 1234567.89m, "R$ 1.234.567,89" },
        { 0.05m, "R$ 0,05" },
        { 99.999m, "R$ 100,00" },
    };

    public static TheoryData<decimal, string> Quantities => new()
    {
        { 12.5m, "12,5" },
        { 12.345m, "12,345" },
        { 100m, "100" },
        { 1234.5m, "1.234,5" },
        { 0m, "0" },
    };

    [Theory]
    [MemberData(nameof(Amounts))]
    public void Money_UsesTheBrazilianFormat(decimal amount, string expected)
    {
        InvoiceText.Money(amount).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(Quantities))]
    public void Cubic_ShowsUpToThreeDecimalsWithoutTrailingZeros(decimal amount, string expected)
    {
        InvoiceText.Cubic(amount).Should().Be(expected);
    }

    [Theory]
    [InlineData(2026, 1, "Janeiro de 2026")]
    [InlineData(2026, 3, "Março de 2026")]
    [InlineData(2026, 10, "Outubro de 2026")]
    [InlineData(2027, 12, "Dezembro de 2027")]
    public void Competence_NamesTheMonthInPortuguese(int year, int month, string expected)
    {
        InvoiceText.Competence(YearMonth.Create(year, month).Value).Should().Be(expected);
    }

    [Fact]
    public void Date_UsesDayMonthYearWithLeadingZeros()
    {
        InvoiceText.Date(new DateOnly(2026, 11, 5)).Should().Be("05/11/2026");
    }

    [Theory]
    [InlineData(null, "101", "101")]
    [InlineData("A", "102", "Bloco A · 102")]
    public void Unit_FollowsTheLabelOfTheScreens(string? block, string number, string expected)
    {
        InvoiceText.Unit(block, number).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "101", "fatura-2026-10-101.pdf")]
    [InlineData("A", "102", "fatura-2026-10-bloco-a-102.pdf")]
    [InlineData("Torre Ipê", "12-B", "fatura-2026-10-bloco-torre-ipe-12-b.pdf")]
    [InlineData("A/B", " 7 ", "fatura-2026-10-bloco-a-b-7.pdf")]
    public void FileName_IsASafeSlug(string? block, string number, string expected)
    {
        InvoiceText.FileName(YearMonth.Create(2026, 10).Value, block, number).Should().Be(expected);
    }

    [Fact]
    public void Reference_JoinsTheMonthAndTheEndOfTheChargeId()
    {
        Guid chargeId = Guid.Parse("018f0000-0000-7000-8000-0000abcdef12");

        InvoiceText.Reference(YearMonth.Create(2026, 10).Value, chargeId).Should().Be("2026-10-ABCDEF12");
    }

    [Theory]
    [InlineData(60, "60%")]
    [InlineData(2.5, "2,5%")]
    [InlineData(33.33333333, "33,3333%")]
    [InlineData(0.00004, "0%")]
    [InlineData(100, "100%")]
    [InlineData(1234.5, "1.234,5%")]
    public void Percent_ShowsUpToFourDecimalsWithoutTrailingZeros(double percent, string expected)
    {
        InvoiceText.Percent((decimal)percent).Should().Be(expected);
    }

    [Theory]
    [InlineData(ApportionmentBasisKind.IdealFraction, "Fração ideal")]
    [InlineData(ApportionmentBasisKind.EqualShare, "Partes iguais")]
    [InlineData(ApportionmentBasisKind.Area, "Área")]
    public void Method_NamesTheBasisOfTheApportionment(ApportionmentBasisKind kind, string expected)
    {
        InvoiceText.Method(kind).Should().Be(expected);
    }

    [Theory]
    [InlineData(ChargeStatus.Open, "Em aberto")]
    [InlineData(ChargeStatus.Partial, "Paga em parte")]
    [InlineData(ChargeStatus.Paid, "Paga")]
    [InlineData(ChargeStatus.Overdue, "Vencida")]
    public void Status_UsesTheWordsOfTheScreens(ChargeStatus status, string expected)
    {
        InvoiceText.Status(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(PixKeyKind.Cnpj, "CNPJ")]
    [InlineData(PixKeyKind.Cpf, "CPF")]
    [InlineData(PixKeyKind.Email, "E-mail")]
    [InlineData(PixKeyKind.Phone, "Celular")]
    [InlineData(PixKeyKind.Random, "Chave aleatória")]
    public void PixKind_NamesTheTypeOfTheKey(PixKeyKind kind, string expected)
    {
        InvoiceText.PixKind(kind).Should().Be(expected);
    }

    [Theory]
    [InlineData(PixKeyKind.Cnpj, "12345678000195", "12.345.678/0001-95")]
    [InlineData(PixKeyKind.Cnpj, "12ABC34501DE35", "12.ABC.345/01DE-35")]
    [InlineData(PixKeyKind.Cpf, "12345678909", "123.456.789-09")]
    [InlineData(PixKeyKind.Phone, "+5547991234567", "+55 (47) 99123-4567")]
    [InlineData(PixKeyKind.Email, "financeiro@aurora.com.br", "financeiro@aurora.com.br")]
    [InlineData(PixKeyKind.Random, "123e4567-e89b-12d3-a456-426614174000", "123e4567-e89b-12d3-a456-426614174000")]
    public void PixKey_IsShownTheWayPeopleWriteIt(PixKeyKind kind, string key, string expected)
    {
        InvoiceText.PixKey(kind, key).Should().Be(expected);
    }

    [Theory]
    [InlineData(PixKeyKind.Cnpj, "123")]
    [InlineData(PixKeyKind.Cpf, "123")]
    [InlineData(PixKeyKind.Phone, "123")]
    public void PixKey_ThatDoesNotFitTheShape_IsLeftAsItIs(PixKeyKind kind, string key)
    {
        InvoiceText.PixKey(kind, key).Should().Be(key);
    }

    [Theory]
    [InlineData(BankAccountKind.Checking, "Conta corrente")]
    [InlineData(BankAccountKind.Savings, "Conta poupança")]
    public void AccountKind_NamesTheTypeOfTheAccount(BankAccountKind kind, string expected)
    {
        InvoiceText.AccountKind(kind).Should().Be(expected);
    }
}
