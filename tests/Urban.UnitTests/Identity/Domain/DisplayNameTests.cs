using FluentAssertions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.UnitTests.Identity.Domain;

public class DisplayNameTests
{
    [Theory]
    [InlineData("Ana", "Ana")]
    [InlineData("  Ana   Paula ", "Ana Paula")]
    [InlineData("Zé", "Zé")]
    [InlineData("D'Ávila", "D'Ávila")]
    [InlineData("João-Pedro", "João-Pedro")]
    [InlineData("Ana P.", "Ana P.")]
    public void Create_CollapsesTheSpacesAndKeepsTheAccents(string input, string expected)
    {
        Result<DisplayName> result = DisplayName.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
        result.Value.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    [InlineData("A.")]
    [InlineData("..")]
    [InlineData("- -")]
    [InlineData("Ana 2")]
    [InlineData("Ana!")]
    [InlineData("<b>Ana</b>")]
    [InlineData("Ana\u0007")]
    public void Create_WithoutTwoLettersOrWithOddCharacters_Fails(string? input)
    {
        Result<DisplayName> result = DisplayName.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DisplayName.Invalid);
        DisplayName.Invalid.Code.Should().Be("identity.display_name.invalid");
    }

    [Fact]
    public void Create_AcceptsUpTo40Characters_AndRefusesMore()
    {
        DisplayName.Create(new string('a', 40)).IsSuccess.Should().BeTrue();
        DisplayName.Create(new string('a', 41)).IsFailure.Should().BeTrue();
        DisplayName.MaxLength.Should().Be(40);
    }

    [Fact]
    public void Create_AcceptsTwoCharacters()
    {
        DisplayName.Create("Jo").IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void DisplayNames_WithTheSameText_AreEqual()
    {
        DisplayName.Create("Ana  Paula").Value.Should().Be(DisplayName.Create("Ana Paula").Value);
    }
}
