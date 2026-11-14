using FluentAssertions;
using Urban.Identity.Domain.Tokens;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Domain;

/// <summary>O convite do morador usa o mesmo link da confirmação de e-mail, mas vale 72 horas: o morador não está esperando o e-mail na hora.</summary>
public class EmailVerificationInvitationTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void IssueInvitation_ExpiresIn72Hours_AndStartsUnused()
    {
        EmailVerificationToken token = EmailVerificationToken.IssueInvitation(UserId, "hash", Now);

        token.UserId.Should().Be(UserId);
        token.TokenHash.Should().Be("hash");
        token.CreatedAt.Should().Be(Now);
        token.ExpiresAt.Should().Be(Now.AddHours(72));
        token.UsedAt.Should().BeNull();
        EmailVerificationToken.InvitationLifetime.Should().Be(TimeSpan.FromHours(72));
    }

    [Fact]
    public void Issue_KeepsTheShortLifetimeOfTheSelfRegistrationLink()
    {
        EmailVerificationToken.Issue(UserId, "hash", Now).ExpiresAt.Should().Be(Now.AddHours(24));
    }

    [Fact]
    public void Consume_OfAnInvitation_WorksUntilTheLastSecondOf72Hours()
    {
        EmailVerificationToken token = EmailVerificationToken.IssueInvitation(UserId, "hash", Now);

        Result result = token.Consume(Now.AddHours(72).AddTicks(-1));

        result.IsSuccess.Should().BeTrue();
        token.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public void Consume_OfAnInvitation_AtTheExpiryInstantFailsAsExpired()
    {
        EmailVerificationToken token = EmailVerificationToken.IssueInvitation(UserId, "hash", Now);

        Result result = token.Consume(Now.AddHours(72));

        result.Error.Should().Be(EmailVerificationTokenErrors.Expired);
    }

    [Fact]
    public void IssueInvitation_WithoutAHash_Throws()
    {
        Action issue = () => EmailVerificationToken.IssueInvitation(UserId, " ", Now);

        issue.Should().Throw<ArgumentException>();
    }
}
