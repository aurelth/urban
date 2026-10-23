using FluentAssertions;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.UnitTests.Identity.Domain;

public class EmailTests
{
    [Fact]
    public void Create_TrimsAndLowercasesTheAddress()
    {
        Result<Email> result = Email.Create("  Ana.Silva@Example.COM ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("ana.silva@example.com");
    }

    [Fact]
    public void Create_SameAddressWithDifferentCase_ProducesEqualEmails()
    {
        Email first = Email.Create("ana@example.com").Value;
        Email second = Email.Create("ANA@Example.com").Value;

        first.Should().Be(second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("a@")]
    [InlineData("@b.com")]
    [InlineData("a@b")]
    [InlineData("a b@c.com")]
    [InlineData("a@@b.com")]
    [InlineData("a@b..com")]
    [InlineData("a@.com")]
    [InlineData("a@b.")]
    public void Create_WithInvalidFormat_Fails(string? address)
    {
        Result<Email> result = Email.Create(address);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Email.Invalid);
    }

    [Fact]
    public void Create_LongerThan254Characters_Fails()
    {
        string address = new string('a', 250) + "@b.com";

        Result<Email> result = Email.Create(address);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Email.Invalid);
    }
}
