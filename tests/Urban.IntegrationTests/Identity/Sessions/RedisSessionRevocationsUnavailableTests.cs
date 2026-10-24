using FluentAssertions;
using StackExchange.Redis;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Infrastructure.Sessions;
using Urban.Identity.Infrastructure.Tokens;
using Urban.TestSupport.Identity;
using Urban.TestSupport.Time;

namespace Urban.IntegrationTests.Identity.Sessions;

public sealed class RedisSessionRevocationsUnavailableTests : IDisposable
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect(
        ConfigurationOptions.Parse("127.0.0.1:1,abortConnect=false,connectTimeout=500,syncTimeout=500,asyncTimeout=500"));

    private readonly RedisSessionRevocations _sessions;

    public RedisSessionRevocationsUnavailableTests() =>
        _sessions = new RedisSessionRevocations(_redis, new FakeClock(Now), new JwtOptions());

    public void Dispose() => _redis.Dispose();

    [Fact]
    public async Task Checking_a_token_reports_an_unavailable_store()
    {
        Func<Task> check = () => _sessions.IsRevokedAsync("jti", Guid.CreateVersion7(), Now, CancellationToken.None);

        await check.Should().ThrowAsync<SessionStoreUnavailableException>();
    }

    [Fact]
    public async Task Revoking_an_access_token_reports_an_unavailable_store()
    {
        Func<Task> revoke = () => _sessions.RevokeAccessTokenAsync("jti", Now.AddMinutes(10), CancellationToken.None);

        await revoke.Should().ThrowAsync<SessionStoreUnavailableException>();
    }

    [Fact]
    public async Task Revoking_every_session_of_a_user_reports_an_unavailable_store()
    {
        Func<Task> revoke = () => _sessions.RevokeAllOfUserAsync(Guid.CreateVersion7(), Now, CancellationToken.None);

        await revoke.Should().ThrowAsync<SessionStoreUnavailableException>();
    }
}