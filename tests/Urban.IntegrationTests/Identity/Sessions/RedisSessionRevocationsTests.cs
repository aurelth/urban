using FluentAssertions;
using Urban.Identity.Infrastructure.Sessions;
using Urban.Identity.Infrastructure.Tokens;
using Urban.TestSupport.Identity;
using Urban.TestSupport.Time;

namespace Urban.IntegrationTests.Identity.Sessions;

[Collection(RedisCollection.Name)]
public sealed class RedisSessionRevocationsTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Now = UserBuilder.Now;
    private static readonly JwtOptions Jwt = new();

    private readonly RedisFixture _redis;
    private readonly RedisSessionRevocations _sessions;

    public RedisSessionRevocationsTests(RedisFixture redis)
    {
        _redis = redis;
        _sessions = new RedisSessionRevocations(redis.Connection, new FakeClock(Now), Jwt);
    }

    [Fact]
    public async Task A_token_nobody_revoked_is_not_revoked()
    {
        bool revoked = await _sessions.IsRevokedAsync(NewJti(), Guid.CreateVersion7(), Now, Ct);

        revoked.Should().BeFalse();
    }

    [Fact]
    public async Task A_revoked_access_token_is_reported_as_revoked()
    {
        string jti = NewJti();

        await _sessions.RevokeAccessTokenAsync(jti, Now.AddMinutes(10), Ct);

        (await _sessions.IsRevokedAsync(jti, Guid.CreateVersion7(), Now, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Revoking_one_access_token_does_not_revoke_another()
    {
        await _sessions.RevokeAccessTokenAsync(NewJti(), Now.AddMinutes(10), Ct);

        (await _sessions.IsRevokedAsync(NewJti(), Guid.CreateVersion7(), Now, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task The_revoked_token_entry_lives_only_until_the_token_would_expire()
    {
        string jti = NewJti();

        await _sessions.RevokeAccessTokenAsync(jti, Now.AddMinutes(10), Ct);

        TimeSpan ttl = (await _redis.Database.KeyTimeToLiveAsync(RedisSessionRevocations.TokenKey(jti)))!.Value;
        ttl.Should().BeGreaterThan(TimeSpan.FromMinutes(9)).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task A_token_that_already_expired_is_not_stored()
    {
        string jti = NewJti();

        await _sessions.RevokeAccessTokenAsync(jti, Now.AddSeconds(-1), Ct);

        (await _redis.Database.KeyExistsAsync(RedisSessionRevocations.TokenKey(jti))).Should().BeFalse();
    }

    [Theory]
    [InlineData(-30, true)]
    [InlineData(0, true)]
    [InlineData(30, false)]
    public async Task Revoking_all_sessions_covers_tokens_issued_up_to_that_instant(int issuedOffsetSeconds, bool expectedRevoked)
    {
        Guid userId = Guid.CreateVersion7();
        await _sessions.RevokeAllOfUserAsync(userId, Now, Ct);

        bool revoked = await _sessions.IsRevokedAsync(NewJti(), userId, Now.AddSeconds(issuedOffsetSeconds), Ct);

        revoked.Should().Be(expectedRevoked);
    }

    [Fact]
    public async Task Revoking_all_sessions_of_one_user_leaves_other_users_alone()
    {
        await _sessions.RevokeAllOfUserAsync(Guid.CreateVersion7(), Now, Ct);

        (await _sessions.IsRevokedAsync(NewJti(), Guid.CreateVersion7(), Now, Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task The_user_entry_lives_as_long_as_an_access_token_can()
    {
        Guid userId = Guid.CreateVersion7();

        await _sessions.RevokeAllOfUserAsync(userId, Now, Ct);

        TimeSpan ttl = (await _redis.Database.KeyTimeToLiveAsync(RedisSessionRevocations.UserKey(userId)))!.Value;
        ttl.Should().BeGreaterThan(Jwt.AccessTokenLifetime - TimeSpan.FromMinutes(1)).And.BeLessThanOrEqualTo(Jwt.AccessTokenLifetime);
    }

    [Fact]
    public async Task A_user_revocation_older_than_any_live_token_is_not_stored()
    {
        Guid userId = Guid.CreateVersion7();

        await _sessions.RevokeAllOfUserAsync(userId, Now - Jwt.AccessTokenLifetime - TimeSpan.FromMinutes(1), Ct);

        (await _redis.Database.KeyExistsAsync(RedisSessionRevocations.UserKey(userId))).Should().BeFalse();
    }

    [Fact]
    public async Task A_later_revocation_replaces_an_earlier_one()
    {
        Guid userId = Guid.CreateVersion7();
        await _sessions.RevokeAllOfUserAsync(userId, Now, Ct);

        await _sessions.RevokeAllOfUserAsync(userId, Now.AddSeconds(30), Ct);

        (await _sessions.IsRevokedAsync(NewJti(), userId, Now.AddSeconds(30), Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task An_earlier_revocation_never_undoes_a_later_one()
    {
        Guid userId = Guid.CreateVersion7();
        await _sessions.RevokeAllOfUserAsync(userId, Now.AddSeconds(30), Ct);

        await _sessions.RevokeAllOfUserAsync(userId, Now, Ct);

        (await _sessions.IsRevokedAsync(NewJti(), userId, Now.AddSeconds(30), Ct)).Should().BeTrue();
    }

    private static string NewJti() => Guid.CreateVersion7().ToString();
}