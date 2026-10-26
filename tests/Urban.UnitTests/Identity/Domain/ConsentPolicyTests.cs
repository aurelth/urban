using FluentAssertions;
using Urban.Identity.Domain.Consent;
using Urban.SharedKernel;

namespace Urban.UnitTests.Identity.Domain;

public class ConsentPolicyTests
{
    private const string Current = "2026-10-26";

    [Fact]
    public void Validate_WithTheCurrentVersion_Succeeds()
    {
        Result result = ConsentPolicy.Validate(Current, Current);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithoutAVersion_FailsAsRequired(string? accepted)
    {
        Result result = ConsentPolicy.Validate(accepted, Current);

        result.Error.Should().Be(ConsentErrors.Required);
    }

    [Theory]
    [InlineData("2025-01-01")]
    [InlineData("2026-10-27")]
    [InlineData(" 2026-10-26")]
    [InlineData("2026-10-26 ")]
    public void Validate_WithAnyOtherVersion_FailsAsOutdated(string accepted)
    {
        Result result = ConsentPolicy.Validate(accepted, Current);

        result.Error.Should().Be(ConsentErrors.Outdated);
    }

    [Fact]
    public void Validate_ComparesTheVersionsCaseSensitively()
    {
        Result result = ConsentPolicy.Validate("V2026-A", "v2026-a");

        result.Error.Should().Be(ConsentErrors.Outdated);
    }
}