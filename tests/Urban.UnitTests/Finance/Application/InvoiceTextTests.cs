using FluentAssertions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain.Months;

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
}
