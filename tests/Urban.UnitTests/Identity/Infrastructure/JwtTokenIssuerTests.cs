using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Tokens;
using Urban.TestSupport.Identity;

namespace Urban.UnitTests.Identity.Infrastructure;

public class JwtTokenIssuerTests
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static readonly JwtOptions Options = new()
    {
        Issuer = "urban-test",
        Audience = "urban-web-test",
        SigningKey = SigningKey,
    };

    private readonly JwtTokenIssuer _issuer = new(Options);

    public static TheoryData<string> InvalidSigningKeys => new()
    {
        string.Empty,
        "   ",
        "isto não é base64!",
        Convert.ToBase64String(new byte[16]),
    };

    [Fact]
    public async Task IssueAccessToken_ForAManager_ProducesASignedTokenWithTheExpectedClaims()
    {
        User manager = UserBuilder.InStatus(AccountStatus.Active, UserRole.Manager);

        AccessToken token = _issuer.IssueAccessToken(manager, Now);

        TokenValidationResult result = await Validate(token.Value, SigningKey);
        result.IsValid.Should().BeTrue();
        result.Claims["sub"].Should().Be(manager.Id.ToString());
        result.Claims["role"].Should().Be("Manager");
        result.Claims["condominium_id"].Should().Be(manager.CondominiumId.ToString());
        result.Claims["jti"].ToString().Should().NotBeNullOrWhiteSpace();
        token.ExpiresAt.Should().Be(Now.AddMinutes(15));
        ((JsonWebToken)result.SecurityToken).ValidTo.Should().Be(Now.AddMinutes(15).UtcDateTime);
        ((JsonWebToken)result.SecurityToken).ValidFrom.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public async Task IssueAccessToken_ForTheSuperAdmin_HasNoCondominiumClaim()
    {
        User superAdmin = UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin);

        AccessToken token = _issuer.IssueAccessToken(superAdmin, Now);

        TokenValidationResult result = await Validate(token.Value, SigningKey);
        result.IsValid.Should().BeTrue();
        result.Claims["role"].Should().Be("SuperAdmin");
        result.Claims.Should().NotContainKey("condominium_id");
    }

    [Fact]
    public async Task IssueAccessToken_GivesEachTokenItsOwnJti()
    {
        User user = UserBuilder.InStatus(AccountStatus.Active);

        TokenValidationResult first = await Validate(_issuer.IssueAccessToken(user, Now).Value, SigningKey);
        TokenValidationResult second = await Validate(_issuer.IssueAccessToken(user, Now).Value, SigningKey);

        first.Claims["jti"].Should().NotBe(second.Claims["jti"]);
    }

    [Fact]
    public async Task IssueAccessToken_IsRejectedWhenCheckedWithAnotherKey()
    {
        AccessToken token = _issuer.IssueAccessToken(UserBuilder.InStatus(AccountStatus.Active), Now);

        TokenValidationResult result = await Validate(token.Value, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(InvalidSigningKeys))]
    public void Constructor_WithAnInvalidSigningKey_Throws(string signingKey)
    {
        Action act = () => _ = new JwtTokenIssuer(new JwtOptions { SigningKey = signingKey });

        act.Should().Throw<InvalidOperationException>().WithMessage("*chave de assinatura*");
    }

    private static Task<TokenValidationResult> Validate(string token, string signingKey) =>
        new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(signingKey)),
                ValidateLifetime = false,
            });
}
