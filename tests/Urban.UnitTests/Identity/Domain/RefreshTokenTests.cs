using FluentAssertions;
using Urban.Identity.Domain.Tokens;
using Urban.SharedKernel;

namespace Urban.UnitTests.Identity.Domain;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Issue_ExpiresSevenDaysAfterNow()
    {
        RefreshToken token = RefreshToken.Issue(UserId, "hash-1", Now);

        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash-1");
        token.ExpiresAt.Should().Be(Now.AddDays(7));
        token.RevokedAt.Should().BeNull();
        token.ReplacedById.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_WithBlankHash_Throws(string hash)
    {
        Action act = () => RefreshToken.Issue(UserId, hash, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsActive_IsTrueUntilTheExpirationInstant()
    {
        RefreshToken token = RefreshToken.Issue(UserId, "hash-1", Now);

        token.IsActive(Now).Should().BeTrue();
        token.IsActive(Now.AddDays(7).AddSeconds(-1)).Should().BeTrue();
        token.IsActive(Now.AddDays(7)).Should().BeFalse();
    }

    [Fact]
    public void Rotate_OnAnActiveToken_IssuesAReplacementAndRevokesTheCurrent()
    {
        RefreshToken current = RefreshToken.Issue(UserId, "hash-1", Now);
        DateTimeOffset later = Now.AddHours(1);

        Result<RefreshToken> result = current.Rotate("hash-2", later);

        result.IsSuccess.Should().BeTrue();
        RefreshToken replacement = result.Value;
        replacement.UserId.Should().Be(UserId);
        replacement.Id.Should().NotBe(current.Id);
        replacement.TokenHash.Should().Be("hash-2");
        replacement.ExpiresAt.Should().Be(later.AddDays(7));
        replacement.IsActive(later).Should().BeTrue();
        current.RevokedAt.Should().Be(later);
        current.ReplacedById.Should().Be(replacement.Id);
        current.IsActive(later).Should().BeFalse();
    }

    [Fact]
    public void Rotate_OnAnExpiredToken_FailsWithExpiredAndKeepsItUntouched()
    {
        RefreshToken current = RefreshToken.Issue(UserId, "hash-1", Now);

        Result<RefreshToken> result = current.Rotate("hash-2", Now.AddDays(8));

        result.Error.Should().Be(RefreshTokenErrors.Expired);
        current.RevokedAt.Should().BeNull();
        current.ReplacedById.Should().BeNull();
    }

    [Fact]
    public void Rotate_OnARevokedToken_FailsWithReused()
    {
        RefreshToken current = RefreshToken.Issue(UserId, "hash-1", Now);
        current.Revoke(Now.AddMinutes(1));

        Result<RefreshToken> result = current.Rotate("hash-2", Now.AddMinutes(2));

        result.Error.Should().Be(RefreshTokenErrors.Reused);
    }

    [Fact]
    public void Rotate_OnATokenThatWasAlreadyRotated_FailsWithReused()
    {
        RefreshToken current = RefreshToken.Issue(UserId, "hash-1", Now);
        current.Rotate("hash-2", Now.AddMinutes(1));

        Result<RefreshToken> secondAttempt = current.Rotate("hash-3", Now.AddMinutes(2));

        secondAttempt.Error.Should().Be(RefreshTokenErrors.Reused);
    }

    [Fact]
    public void Rotate_OnATokenThatIsRevokedAndExpired_FailsWithReusedBecauseItIsTheMoreSevere()
    {
        RefreshToken current = RefreshToken.Issue(UserId, "hash-1", Now);
        current.Revoke(Now.AddMinutes(1));

        Result<RefreshToken> result = current.Rotate("hash-2", Now.AddDays(30));

        result.Error.Should().Be(RefreshTokenErrors.Reused);
    }

    [Fact]
    public void Revoke_IsIdempotentAndKeepsTheFirstInstant()
    {
        RefreshToken token = RefreshToken.Issue(UserId, "hash-1", Now);
        DateTimeOffset first = Now.AddMinutes(1);

        token.Revoke(first);
        token.Revoke(Now.AddMinutes(5));

        token.RevokedAt.Should().Be(first);
        token.IsActive(first).Should().BeFalse();
    }
}
