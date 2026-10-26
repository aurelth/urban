using FluentAssertions;
using Urban.Identity.Domain.Consent;
using Urban.Identity.Infrastructure.Consent;

namespace Urban.UnitTests.Identity.Infrastructure;

public class ConsentOptionsTests
{
    [Theory]
    [InlineData("2026-10-26")]
    [InlineData("v2")]
    public void GetCurrentVersion_ReturnsTheConfiguredVersion(string configured)
    {
        ConsentOptions options = new() { CurrentVersion = configured };

        options.GetCurrentVersion().Should().Be(configured);
    }

    [Fact]
    public void GetCurrentVersion_AcceptsAVersionOfExactlyTheMaximumLength()
    {
        string configured = new('v', UserConsent.MaxVersionLength);
        ConsentOptions options = new() { CurrentVersion = configured };

        options.GetCurrentVersion().Should().Be(configured);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCurrentVersion_WithoutAVersion_Throws(string configured)
    {
        ConsentOptions options = new() { CurrentVersion = configured };

        Action read = () => options.GetCurrentVersion();

        read.Should().Throw<InvalidOperationException>().WithMessage("*Consent:CurrentVersion*");
    }

    [Fact]
    public void GetCurrentVersion_WithAVersionLongerThanTheMaximum_Throws()
    {
        ConsentOptions options = new() { CurrentVersion = new string('v', UserConsent.MaxVersionLength + 1) };

        Action read = () => options.GetCurrentVersion();

        read.Should().Throw<InvalidOperationException>().WithMessage("*Consent:CurrentVersion*");
    }
}