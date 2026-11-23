using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Urban.Api.Hubs;
using Urban.Identity.Application.Abstractions;

namespace Urban.IntegrationTests.Api;

public class SubjectUserIdProviderTests
{
    private static HubConnectionContext ConnectionOf(ClaimsPrincipal user) =>
        new(new DefaultConnectionContext("connection-1") { User = user }, new HubConnectionContextOptions(), NullLoggerFactory.Instance);

    private static ClaimsPrincipal Token(Guid userId) =>
        new(new ClaimsIdentity(
            [
                new Claim(AccessTokenClaims.Subject, userId.ToString()),
                new Claim(AccessTokenClaims.TokenId, "token-1"),
                new Claim(AccessTokenClaims.IssuedAt, "1795000000"),
                new Claim(AccessTokenClaims.ExpiresAt, "1795000900"),
            ],
            "Bearer"));

    [Fact]
    public void The_user_of_a_connection_is_the_account_in_the_access_token()
    {
        Guid userId = Guid.CreateVersion7();

        new SubjectUserIdProvider().GetUserId(ConnectionOf(Token(userId))).Should().Be(userId.ToString());
    }

    [Fact]
    public void A_connection_without_a_session_has_no_user()
    {
        new SubjectUserIdProvider().GetUserId(ConnectionOf(new ClaimsPrincipal(new ClaimsIdentity()))).Should().BeNull();
    }

    [Fact]
    public void A_token_that_is_not_a_session_has_no_user()
    {
        ClaimsPrincipal stranger = new(new ClaimsIdentity([new Claim(AccessTokenClaims.Subject, Guid.CreateVersion7().ToString())], "Bearer"));

        new SubjectUserIdProvider().GetUserId(ConnectionOf(stranger)).Should().BeNull();
    }
}
