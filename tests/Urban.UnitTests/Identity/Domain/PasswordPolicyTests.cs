using FluentAssertions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.UnitTests.Identity.Domain;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("senha12345")]
    [InlineData("Abcdefghi1")]
    [InlineData("ação-segura-2026")]
    public void Validate_WithLettersAndDigitsAndEnoughLength_Succeeds(string password)
    {
        Result result = PasswordPolicy.Validate(password);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc123456")]
    public void Validate_ShorterThanTheMinimum_FailsWithTooShort(string? password)
    {
        Result result = PasswordPolicy.Validate(password);

        result.Error.Should().Be(PasswordPolicy.TooShort);
    }

    [Fact]
    public void Validate_LongerThanTheMaximum_FailsWithTooLong()
    {
        string password = "a1" + new string('x', PasswordPolicy.MaxLength);

        Result result = PasswordPolicy.Validate(password);

        result.Error.Should().Be(PasswordPolicy.TooLong);
    }

    [Theory]
    [InlineData("abcdefghij")]
    [InlineData("1234567890")]
    [InlineData("!!!!!!!!!!!!")]
    public void Validate_WithoutLetterOrWithoutDigit_FailsWithNeedsLetterAndDigit(string password)
    {
        Result result = PasswordPolicy.Validate(password);

        result.Error.Should().Be(PasswordPolicy.NeedsLetterAndDigit);
    }

    [Fact]
    public void Validate_ExactlyAtTheLimits_Succeeds()
    {
        string atMinimum = "a1" + new string('x', PasswordPolicy.MinLength - 2);
        string atMaximum = "a1" + new string('x', PasswordPolicy.MaxLength - 2);

        PasswordPolicy.Validate(atMinimum).IsSuccess.Should().BeTrue();
        PasswordPolicy.Validate(atMaximum).IsSuccess.Should().BeTrue();
    }
}
