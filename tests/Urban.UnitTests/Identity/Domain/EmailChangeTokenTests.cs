using FluentAssertions;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel;

namespace Urban.UnitTests.Identity.Domain;

public class EmailChangeTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Email NewEmail = Email.Create("nova@example.com").Value;

    [Fact]
    public void Issue_KeepsTheNewAddressAndTheHash_AndExpiresIn24Hours()
    {
        Guid userId = Guid.CreateVersion7();

        EmailChangeToken token = EmailChangeToken.Issue(userId, NewEmail, "hash", Now);

        token.UserId.Should().Be(userId);
        token.NewEmail.Should().Be(NewEmail);
        token.TokenHash.Should().Be("hash");
        token.CreatedAt.Should().Be(Now);
        token.ExpiresAt.Should().Be(Now.AddHours(24));
        token.UsedAt.Should().BeNull();
    }

    [Fact]
    public void Consume_BeforeExpiry_MarksTheLinkAsUsed()
    {
        EmailChangeToken token = EmailChangeToken.Issue(Guid.CreateVersion7(), NewEmail, "hash", Now);

        Result result = token.Consume(Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        token.UsedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Consume_AfterTheExpiry_FailsAsExpired()
    {
        EmailChangeToken token = EmailChangeToken.Issue(Guid.CreateVersion7(), NewEmail, "hash", Now);

        token.Consume(Now.AddHours(24)).Error.Should().Be(EmailChangeTokenErrors.Expired);
        token.UsedAt.Should().BeNull();
    }

    [Fact]
    public void Consume_Twice_FailsTheSecondTimeAsInvalid()
    {
        EmailChangeToken token = EmailChangeToken.Issue(Guid.CreateVersion7(), NewEmail, "hash", Now);
        token.Consume(Now.AddMinutes(1));

        token.Consume(Now.AddMinutes(2)).Error.Should().Be(EmailChangeTokenErrors.Invalid);
    }

    [Fact]
    public void TheErrors_HaveStableCodes()
    {
        EmailChangeTokenErrors.Invalid.Code.Should().Be("identity.email_change.invalid");
        EmailChangeTokenErrors.Expired.Code.Should().Be("identity.email_change.expired");
    }

    [Fact]
    public void IsAnEmailLink_SoItSharesTheOneEmailPerMinuteRule()
    {
        EmailChangeToken token = EmailChangeToken.Issue(Guid.CreateVersion7(), NewEmail, "hash", Now);

        token.Should().BeAssignableTo<EmailLinkToken>();
        token.WasIssuedRecently(Now.AddSeconds(30)).Should().BeTrue();
        token.WasIssuedRecently(Now.AddMinutes(2)).Should().BeFalse();
    }
}
