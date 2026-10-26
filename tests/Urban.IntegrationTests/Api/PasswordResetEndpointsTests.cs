using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.Identity.Infrastructure.Tokens;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class PasswordResetEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string OldPassword = "Senha-antiga-123";
    private const string NewPassword = "Senha-nova-456";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    private readonly ISecretGenerator _secrets = new SecretGenerator();

    private RecordingEmailSender Emails => fixture.Factory.Emails;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Forgot_for_an_active_account_returns_202_and_sends_one_email_with_a_link_to_the_reset_screen()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);

        HttpResponseMessage response = await ForgotAsync(user.Email.Value);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        EmailMessage message = Emails.SentTo(user.Email.Value).Should().ContainSingle().Subject;
        message.Subject.Should().Be("Redefina sua senha no Urban");
        message.TextBody.Should().Contain("https://app.test/redefinir-senha?token=");
        RecordingEmailSender.TokenIn(message).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Forgot_stores_only_the_hash_of_the_link_secret()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        await ForgotAsync(user.Email.Value);
        string secret = RecordingEmailSender.TokenIn(Emails.SentTo(user.Email.Value).Single());

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        string[] stored = await db.PasswordResetTokens.AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .Select(t => t.TokenHash)
            .ToArrayAsync();

        stored.Should().ContainSingle().Which.Should().Be(_secrets.Hash(secret)).And.NotBe(secret);
    }

    [Fact]
    public async Task Forgot_twice_in_a_row_sends_only_one_email()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);

        HttpResponseMessage first = await ForgotAsync(user.Email.Value);
        HttpResponseMessage second = await ForgotAsync(user.Email.Value.ToUpperInvariant());

        second.StatusCode.Should().Be(first.StatusCode);
        Emails.SentTo(user.Email.Value).Should().ContainSingle();
    }

    [Fact]
    public async Task Forgot_after_the_resend_interval_sends_a_new_link()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-2));

        HttpResponseMessage response = await ForgotAsync(user.Email.Value);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Emails.SentTo(user.Email.Value).Should().ContainSingle();
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.PasswordResetTokens.CountAsync(t => t.UserId == user.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Forgot_for_an_unknown_email_answers_exactly_like_a_known_one_and_sends_nothing()
    {
        string address = $"{Guid.NewGuid():N}@example.com";

        HttpResponseMessage response = await ForgotAsync(address);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        Emails.SentTo(address).Should().BeEmpty();
    }

    [Theory]
    [InlineData(AccountStatus.PendingVerification)]
    [InlineData(AccountStatus.Deactivated)]
    [InlineData(AccountStatus.Suspended)]
    public async Task Forgot_for_an_account_that_cannot_sign_in_answers_the_same_and_sends_nothing(AccountStatus status)
    {
        User user = await fixture.Postgres.SeedUserAsync(status, UserRole.Manager);

        HttpResponseMessage response = await ForgotAsync(user.Email.Value);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        Emails.SentTo(user.Email.Value).Should().BeEmpty();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"email":""}""")]
    [InlineData("""{"email":"sem-arroba"}""")]
    public async Task Forgot_with_an_invalid_email_returns_400_with_a_stable_code(string json)
    {
        HttpResponseMessage response = await _client.PostAsync("/api/auth/forgot-password", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(Email.Invalid.Code);
    }

    [Fact]
    public async Task Reset_changes_the_password_the_new_one_logs_in_and_the_old_one_does_not()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string token = await RequestTokenAsync(user);

        HttpResponseMessage reset = await ResetAsync(token, NewPassword);

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.LoginAsync(user.Email.Value, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.LoginAsync(user.Email.Value, OldPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reset_stores_a_hash_and_never_the_password()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string token = await RequestTokenAsync(user);

        await ResetAsync(token, NewPassword);

        string stored = (await fixture.Postgres.ReadCredentialAsync(user.Id))!.PasswordHash;
        stored.Should().NotContain(NewPassword).And.StartWith("$2");
    }

    [Fact]
    public async Task Reset_twice_with_the_same_link_fails_the_second_time()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string token = await RequestTokenAsync(user);
        await ResetAsync(token, NewPassword);

        HttpResponseMessage second = await ResetAsync(token, "Outra-senha-789");

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.CodeAsync()).Should().Be(PasswordResetTokenErrors.Invalid.Code);
        (await _client.LoginAsync(user.Email.Value, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_with_a_weak_password_is_refused_without_spending_the_link()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string token = await RequestTokenAsync(user);

        HttpResponseMessage weak = await ResetAsync(token, "curta1");
        HttpResponseMessage retry = await ResetAsync(token, NewPassword);

        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await weak.CodeAsync()).Should().Be(PasswordPolicy.TooShort.Code);
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reset_with_an_unknown_link_returns_400()
    {
        HttpResponseMessage response = await ResetAsync("link-que-nunca-existiu", NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(PasswordResetTokenErrors.Invalid.Code);
    }

    [Fact]
    public async Task Reset_with_an_email_confirmation_link_returns_400()
    {
        User user = await fixture.Postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        GeneratedSecret secret = _secrets.Generate();
        await using (IdentityDbContext db = fixture.Postgres.CreateContext())
        {
            db.EmailVerificationTokens.Add(EmailVerificationToken.Issue(user.Id, secret.Hash, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        HttpResponseMessage response = await ResetAsync(secret.Value, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(PasswordResetTokenErrors.Invalid.Code);
    }

    [Fact]
    public async Task Reset_without_a_body_returns_400_for_the_password()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/reset-password", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(PasswordPolicy.TooShort.Code);
    }

    [Fact]
    public async Task Reset_with_an_expired_link_returns_400_and_keeps_the_old_password()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string secret = await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddHours(-2));

        HttpResponseMessage response = await ResetAsync(secret, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(PasswordResetTokenErrors.Expired.Code);
        (await _client.LoginAsync(user.Email.Value, OldPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_with_a_link_that_is_still_valid_just_before_the_hour_ends_works()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string secret = await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-59));

        HttpResponseMessage response = await ResetAsync(secret, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reset_with_a_link_of_an_account_that_became_suspended_returns_400_and_keeps_the_password()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string token = await RequestTokenAsync(user);
        await SuspendAsync(user.Id);
        string hashBefore = (await fixture.Postgres.ReadCredentialAsync(user.Id))!.PasswordHash;

        HttpResponseMessage response = await ResetAsync(token, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(PasswordResetTokenErrors.Invalid.Code);
        (await fixture.Postgres.ReadCredentialAsync(user.Id))!.PasswordHash.Should().Be(hashBefore);
    }

    [Fact]
    public async Task Reset_makes_the_other_links_still_open_for_the_account_useless()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        string olderSecret = await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        string newerSecret = await RequestTokenAsync(user);

        HttpResponseMessage first = await ResetAsync(newerSecret, NewPassword);
        HttpResponseMessage second = await ResetAsync(olderSecret, "Outra-senha-789");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.CodeAsync()).Should().Be(PasswordResetTokenErrors.Invalid.Code);
        (await _client.LoginAsync(user.Email.Value, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_unlocks_an_account_locked_by_wrong_passwords()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        for (int i = 0; i < UserCredential.MaxFailedAttempts; i++)
        {
            await _client.LoginAsync(user.Email.Value, "senha-errada-1");
        }

        (await _client.LoginAsync(user.Email.Value, OldPassword)).StatusCode.Should().Be(HttpStatusCode.Locked);
        string token = await RequestTokenAsync(user);

        await ResetAsync(token, NewPassword);

        (await _client.LoginAsync(user.Email.Value, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        UserCredential credential = (await fixture.Postgres.ReadCredentialAsync(user.Id))!;
        credential.LockoutEnd.Should().BeNull();
        credential.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Reset_ends_the_sessions_that_were_open_before_it()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        HttpResponseMessage login = await _client.LoginAsync(user.Email.Value, OldPassword);
        string cookie = login.RefreshCookieValue()!;
        string accessToken = await login.AccessTokenAsync();
        string token = await RequestTokenAsync(user);

        await ResetAsync(token, NewPassword);

        (await _client.RefreshAsync(cookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.LogoutAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reset_does_not_touch_the_sessions_of_other_people()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        User bystander = await fixture.Postgres.SeedUserWithPasswordAsync(OldPassword);
        HttpResponseMessage bystanderLogin = await _client.LoginAsync(bystander.Email.Value, OldPassword);
        string bystanderCookie = bystanderLogin.RefreshCookieValue()!;
        string bystanderAccess = await bystanderLogin.AccessTokenAsync();
        string token = await RequestTokenAsync(user);

        await ResetAsync(token, NewPassword);

        (await _client.RefreshAsync(bystanderCookie)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.LogoutAsync(bystanderAccess)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) => _client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

    private Task<HttpResponseMessage> ResetAsync(string token, string password) =>
        _client.PostAsJsonAsync("/api/auth/reset-password", new { token, password });

    private async Task<string> RequestTokenAsync(User user)
    {
        await ForgotAsync(user.Email.Value);
        return RecordingEmailSender.TokenIn(Emails.SentTo(user.Email.Value).Last());
    }

    private async Task<string> SeedLinkAsync(Guid userId, DateTimeOffset issuedAt)
    {
        GeneratedSecret secret = _secrets.Generate();
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        db.PasswordResetTokens.Add(PasswordResetToken.Issue(userId, secret.Hash, issuedAt));
        await db.SaveChangesAsync();

        return secret.Value;
    }

    private async Task SuspendAsync(Guid userId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.SingleAsync(u => u.Id == userId);
        user.Suspend(Guid.CreateVersion7(), "Motivo de teste", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }
}