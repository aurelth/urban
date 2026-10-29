using FluentAssertions;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class PersonNameTests
{
    [Theory]
    [InlineData("Ana Silva", "Ana Silva")]
    [InlineData("  Ana   Maria  da Silva ", "Ana Maria da Silva")]
    [InlineData("José D'Ávila-Santos", "José D'Ávila-Santos")]
    [InlineData("Maria J. Oliveira", "Maria J. Oliveira")]
    public void Create_WithNameAndSurname_CollapsesTheSpaces(string input, string expected)
    {
        Result<PersonName> result = PersonName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ana")]
    [InlineData("A B")]
    [InlineData("Ana B")]
    [InlineData("A Silva")]
    [InlineData("Ana 2 Silva")]
    [InlineData("Ana Silva!")]
    [InlineData("Ana @ Silva")]
    public void Create_WithoutNameAndSurnameOrWithOddCharacters_Fails(string? input)
    {
        Result<PersonName> result = PersonName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PersonName.Invalid);
        PersonName.Invalid.Code.Should().Be("condominium.person.full_name_invalid");
    }

    [Fact]
    public void Create_AcceptsUpTo120Characters_AndRefusesMore()
    {
        string within = "Ana " + new string('a', 116);
        string beyond = "Ana " + new string('a', 117);

        PersonName.Create(within).IsSuccess.Should().BeTrue();
        PersonName.Create(beyond).IsFailure.Should().BeTrue();
    }
}
