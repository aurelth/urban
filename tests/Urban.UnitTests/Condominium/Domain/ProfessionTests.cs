using FluentAssertions;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class ProfessionTests
{
    [Fact]
    public void Create_NormalizesTheText()
    {
        Result<Profession> result = Profession.Create("  Engenheira   civil ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("Engenheira civil");
        result.Value.ToString().Should().Be("Engenheira civil");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void Create_WithTooLittleText_Fails(string? text)
    {
        Profession.Create(text).Error.Should().Be(Profession.Invalid);
        Profession.Invalid.Code.Should().Be("condominium.profession.invalid");
    }

    [Fact]
    public void Create_AcceptsTheBoundaries_AndRejectsOneBeyond()
    {
        Profession.Create(new string('a', Profession.MinLength)).IsSuccess.Should().BeTrue();
        Profession.Create(new string('a', Profession.MaxLength)).IsSuccess.Should().BeTrue();
        Profession.Create(new string('a', Profession.MaxLength + 1)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithControlCharacters_Fails()
    {
        Profession.Create("Eng\u0007enheira").Error.Should().Be(Profession.Invalid);
    }
}
