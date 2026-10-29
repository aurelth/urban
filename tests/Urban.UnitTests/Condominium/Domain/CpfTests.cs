using FluentAssertions;
using Urban.Condominium.Domain.Documents;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class CpfTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    [InlineData("  529 982 247 25 ")]
    [InlineData("111.444.777-35")]
    [InlineData("123.456.789-09")]
    public void Create_WithValidNumber_KeepsOnlyTheDigits(string input)
    {
        Result<Cpf> result = Cpf.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().HaveLength(11).And.MatchRegex("^[0-9]{11}$");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("529.982.247-24")]
    [InlineData("529.982.247-26")]
    [InlineData("529.982.247-33")]
    [InlineData("5299822472")]
    [InlineData("529982247255")]
    [InlineData("529.982.247-2A")]
    [InlineData("529,982,247-25")]
    [InlineData("000.000.000-00")]
    [InlineData("111.111.111-11")]
    [InlineData("999.999.999-99")]
    public void Create_WithInvalidNumber_Fails(string? input)
    {
        Result<Cpf> result = Cpf.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Cpf.Invalid);
    }

    [Fact]
    public void Create_RejectsDigitsThatAreNotAscii()
    {
        Result<Cpf> result = Cpf.Create("٥٢٩٩٨٢٢٤٧٢٥");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_SameNumberWithAndWithoutMask_ProducesEqualCpfs()
    {
        Cpf.Create("529.982.247-25").Value.Should().Be(Cpf.Create("52998224725").Value);
    }

    [Fact]
    public void Masked_HidesTheFirstThreeAndTheLastTwoDigits()
    {
        Cpf.Create("52998224725").Value.Masked.Should().Be("***.982.247-**");
    }

    [Fact]
    public void ToString_NeverRevealsTheNumber()
    {
        Cpf cpf = Cpf.Create("52998224725").Value;

        cpf.ToString().Should().NotContain("529").And.NotContain("25");
        $"{cpf}".Should().Be(cpf.Masked);
    }

    [Fact]
    public void Error_HasAStableCodeAndAMessageForThePerson()
    {
        Cpf.Invalid.Code.Should().Be("condominium.cpf.invalid");
        Cpf.Invalid.Description.Should().Be("Informe um CPF válido");
    }
}
