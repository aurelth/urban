using FluentAssertions;
using Urban.Identity.Domain.Consent;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class UserConsentTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Record_KeepsTheUserTheVersionAndTheMoment()
    {
        UserConsent consent = UserConsent.Record(UserId, "2026-10-26", Now);

        consent.UserId.Should().Be(UserId);
        consent.Version.Should().Be("2026-10-26");
        consent.AcceptedAt.Should().Be(Now);
        consent.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Record_GivesEachAcceptanceItsOwnId()
    {
        UserConsent first = UserConsent.Record(UserId, "2026-10-26", Now);
        UserConsent second = UserConsent.Record(UserId, "2026-10-26", Now);

        first.Id.Should().NotBe(second.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Record_WithoutAVersion_Throws(string? version)
    {
        Action record = () => UserConsent.Record(UserId, version!, Now);

        record.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Record_AcceptsAVersionOfExactlyTheMaximumLength()
    {
        string version = new('v', UserConsent.MaxVersionLength);

        UserConsent.Record(UserId, version, Now).Version.Should().Be(version);
    }

    [Fact]
    public void Record_WithAVersionLongerThanTheMaximum_Throws()
    {
        string version = new('v', UserConsent.MaxVersionLength + 1);

        Action record = () => UserConsent.Record(UserId, version, Now);

        record.Should().Throw<ArgumentOutOfRangeException>();
    }
}