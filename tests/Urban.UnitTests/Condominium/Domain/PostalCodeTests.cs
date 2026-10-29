using FluentAssertions;
using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class PostalCodeTests
{
    [Theory]
    [InlineData("89010000")]
    [InlineData("89010-000")]
    [InlineData(" 89.010-000 ")]
    public void Create_WithValidCode_KeepsOnlyTheDigits(string input)
    {
        Result<PostalCode> result = PostalCode.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("89010000");
        result.Value.Formatted.Should().Be("89010-000");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("8901000")]
    [InlineData("890100000")]
    [InlineData("8901A-000")]
    [InlineData("00000-000")]
    public void Create_WithInvalidCode_Fails(string? input)
    {
        Result<PostalCode> result = PostalCode.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PostalCode.Invalid);
        PostalCode.Invalid.Code.Should().Be("condominium.postal_code.invalid");
    }
}
