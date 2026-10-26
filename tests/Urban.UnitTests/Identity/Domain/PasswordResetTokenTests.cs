using FluentAssertions;
using Urban.Identity.Domain.Tokens;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class PasswordResetTokenTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Issue_ExpiresInOneHour_AndStartsUnused()
    {
        PasswordResetToken token = PasswordResetToken.Issue(UserId, "hash", Now);

        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash");
        token.CreatedAt.Should().Be(Now);
        token.ExpiresAt.Should().Be(Now.AddHours(1));
        token.UsedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Issue_WithoutAHash_Throws(string? hash)
    {
        Action issue = () => PasswordResetToken.Issue(UserId, hash!, Now);

        issue.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Consume_BeforeExpiry_MarksTheTokenAsUsed()
    {
        PasswordResetToken token = PasswordResetToken.Issue(UserId, "hash", Now);

        Result result = token.Consume(Now.AddHours(1).AddTicks(-1));

        result.IsSuccess.Should().BeTrue();
        token.UsedAt.Should().Be(Now.AddHours(1).AddTicks(-1));
    }

    [Fact]
    public void Consume_AtTheExpiryInstant_Fails_AndLeavesTheTokenUnused()
    {
        PasswordResetToken token = PasswordResetToken.Issue(UserId, "hash", Now);

        Result result = token.Consume(Now.AddHours(1));

        result.Error.Should().Be(PasswordResetTokenErrors.Expired);
        token.UsedAt.Should().BeNull();
    }

    [Fact]
    public void Consume_Twice_FailsAsInvalid_AndKeepsTheFirstUse()
    {
        PasswordResetToken token = PasswordResetToken.Issue(UserId, "hash", Now);
        token.Consume(Now.AddMinutes(1));

        Result second = token.Consume(Now.AddMinutes(2));

        second.Error.Should().Be(PasswordResetTokenErrors.Invalid);
        token.UsedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Consume_AnUsedToken_ReportsInvalidEvenAfterExpiry()
    {
        PasswordResetToken token = PasswordResetToken.Issue(UserId, "hash", Now);
        token.Consume(Now.AddMinutes(1));

        Result result = token.Consume(Now.AddDays(2));

        result.Error.Should().Be(PasswordResetTokenErrors.Invalid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public void WasIssuedRecently_IsTrueOnlyInsideTheResendInterval(int seconds, bool expected)
    {
        PasswordResetToken token = PasswordResetToken.Issue(UserId, "hash", Now);

        token.WasIssuedRecently(Now.AddSeconds(seconds)).Should().Be(expected);
    }

    [Fact]
    public void TheErrorsHaveTheirOwnStableCodes_SoTheyCannotBeConfusedWithTheEmailConfirmationOnes()
    {
        PasswordResetTokenErrors.Invalid.Code.Should().Be("identity.password_reset.invalid");
        PasswordResetTokenErrors.Expired.Code.Should().Be("identity.password_reset.expired");
    }
}