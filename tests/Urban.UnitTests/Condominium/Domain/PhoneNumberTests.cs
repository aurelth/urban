using FluentAssertions;
using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("47991234567", "47991234567", "(47) 99123-4567", true)]
    [InlineData("(47) 99123-4567", "47991234567", "(47) 99123-4567", true)]
    [InlineData("+55 47 99123-4567", "47991234567", "(47) 99123-4567", true)]
    [InlineData("5547991234567", "47991234567", "(47) 99123-4567", true)]
    [InlineData("(47) 3322-1100", "4733221100", "(47) 3322-1100", false)]
    [InlineData("554733221100", "4733221100", "(47) 3322-1100", false)]
    [InlineData("11 98888-7777", "11988887777", "(11) 98888-7777", true)]
    public void Create_WithValidNumber_KeepsOnlyTheDigitsWithoutTheCountryCode(
        string input,
        string digits,
        string formatted,
        bool isMobile)
    {
        Result<PhoneNumber> result = PhoneNumber.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(digits);
        result.Value.Formatted.Should().Be(formatted);
        result.Value.IsMobile.Should().Be(isMobile);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("991234567")]
    [InlineData("4799123456")]
    [InlineData("479912345678")]
    [InlineData("10991234567")]
    [InlineData("20991234567")]
    [InlineData("47891234567")]
    [InlineData("4711223344")]
    [InlineData("(47) 99123-456A")]
    [InlineData("+1 47 99123-4567")]
    public void Create_WithInvalidNumber_Fails(string? input)
    {
        Result<PhoneNumber> result = PhoneNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PhoneNumber.Invalid);
        PhoneNumber.Invalid.Code.Should().Be("condominium.phone.invalid");
    }
}
