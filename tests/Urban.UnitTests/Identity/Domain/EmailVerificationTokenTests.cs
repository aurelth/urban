using FluentAssertions;
using Urban.Identity.Domain.Tokens;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

public class EmailVerificationTokenTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Issue_ExpiresIn24Hours_AndStartsUnused()
    {
        EmailVerificationToken token = EmailVerificationToken.Issue(UserId, "hash", Now);

        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash");
        token.CreatedAt.Should().Be(Now);
        token.ExpiresAt.Should().Be(Now.AddHours(24));
        token.UsedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Issue_WithoutAHash_Throws(string? hash)
    {
        Action issue = () => EmailVerificationToken.Issue(UserId, hash!, Now);

        issue.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Consume_BeforeExpiry_MarksTheTokenAsUsed()
    {
        EmailVerificationToken token = EmailVerificationToken.Issue(UserId, "hash", Now);

        Result result = token.Consume(Now.AddHours(24).AddTicks(-1));

        result.IsSuccess.Should().BeTrue();
        token.UsedAt.Should().Be(Now.AddHours(24).AddTicks(-1));
    }

    [Fact]
    public void Consume_AtTheExpiryInstant_Fails_AndLeavesTheTokenUnused()
    {
        EmailVerificationToken token = EmailVerificationToken.Issue(UserId, "hash", Now);

        Result result = token.Consume(Now.AddHours(24));

        result.Error.Should().Be(EmailVerificationTokenErrors.Expired);
        token.UsedAt.Should().BeNull();
    }

    [Fact]
    public void Consume_Twice_FailsAsInvalid_AndKeepsTheFirstUse()
    {
        EmailVerificationToken token = EmailVerificationToken.Issue(UserId, "hash", Now);
        token.Consume(Now.AddMinutes(1));

        Result second = token.Consume(Now.AddMinutes(2));

        second.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
        token.UsedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Consume_AnUsedToken_ReportsInvalidEvenAfterExpiry()
    {
        EmailVerificationToken token = EmailVerificationToken.Issue(UserId, "hash", Now);
        token.Consume(Now.AddMinutes(1));

        Result result = token.Consume(Now.AddDays(2));

        result.Error.Should().Be(EmailVerificationTokenErrors.Invalid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public void WasIssuedRecently_IsTrueOnlyInsideTheResendInterval(int seconds, bool expected)
    {
        EmailVerificationToken token = EmailVerificationToken.Issue(UserId, "hash", Now);

        token.WasIssuedRecently(Now.AddSeconds(seconds)).Should().Be(expected);
    }
}