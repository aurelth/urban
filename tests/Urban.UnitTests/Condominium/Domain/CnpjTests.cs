using FluentAssertions;
using Urban.Condominium.Domain.Documents;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class CnpjTests
{
    [Theory]
    [InlineData("11222333000181", "11222333000181")]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    [InlineData(" 11.444.777/0001-61 ", "11444777000161")]
    [InlineData("12ABC34501DE35", "12ABC34501DE35")]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
    [InlineData("12.abc.345/01de-35", "12ABC34501DE35")]
    public void Create_WithValidNumber_KeepsOnlyLettersAndDigitsInUpperCase(string input, string expected)
    {
        Result<Cnpj> result = Cnpj.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("11.222.333/0001-80")]
    [InlineData("11.222.333/0001-91")]
    [InlineData("11.222.333/0001-90")]
    [InlineData("12.ABC.345/01DE-36")]
    [InlineData("12.ABC.345/01DE-3A")]
    [InlineData("12.ABC.345/01D-35")]
    [InlineData("112223330001811")]
    [InlineData("00.000.000/0000-00")]
    [InlineData("11.111.111/1111-11")]
    [InlineData("AA.AAA.AAA/AAAA-AA")]
    [InlineData("11.222.333/0001-8@")]
    public void Create_WithInvalidNumber_Fails(string? input)
    {
        Result<Cnpj> result = Cnpj.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Cnpj.Invalid);
    }

    [Fact]
    public void Create_SameNumberWithAndWithoutMask_ProducesEqualCnpjs()
    {
        Cnpj.Create("11.222.333/0001-81").Value.Should().Be(Cnpj.Create("11222333000181").Value);
    }

    [Theory]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    [InlineData("12ABC34501DE35", "12.ABC.345/01DE-35")]
    public void Formatted_AppliesTheOfficialMask(string input, string expected)
    {
        Cnpj.Create(input).Value.Formatted.Should().Be(expected);
    }

    [Fact]
    public void Error_HasAStableCodeAndAMessageForThePerson()
    {
        Cnpj.Invalid.Code.Should().Be("condominium.cnpj.invalid");
        Cnpj.Invalid.Description.Should().Be("Informe um CNPJ válido");
    }
}
