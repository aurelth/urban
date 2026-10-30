using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.Identity.Infrastructure.Tokens;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class ChangeEmailEndpointsTests(ApiFixture fixture) : IDisposable
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    private readonly ISecretGenerator _secrets = new SecretGenerator();

    private RecordingEmailSender Emails => fixture.Factory.Emails;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Requesting_answers_202_sends_the_link_to_the_new_address_and_keeps_the_current_one_until_confirmed()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();

        HttpResponseMessage response = await RequestAsync(await _client.SignInAsync(user), ManagerSetup.Password, newAddress);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        EmailMessage link = Emails.SentTo(newAddress).Should().ContainSingle().Subject;
        link.Subject.Should().Be("Confirme o novo e-mail da sua conta no Urban");
        link.TextBody.Should().Contain("https://app.test/confirmar-troca-de-email?token=");
        (await _client.LoginAsync(user.Email.Value, ManagerSetup.Password)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.LoginAsync(newAddress, ManagerSetup.Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_current_address_gets_a_notice_that_names_the_new_one_but_has_no_link_to_confirm()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();

        await RequestAsync(await _client.SignInAsync(user), ManagerSetup.Password, newAddress);

        EmailMessage notice = Emails.SentTo(user.Email.Value).Should().ContainSingle().Subject;
        notice.Subject.Should().Be("Pedido para trocar o e-mail da sua conta no Urban");
        notice.TextBody.Should().Contain(newAddress).And.NotContain("token=");
    }

    [Fact]
    public async Task Only_the_hash_of_the_link_secret_is_stored()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();
        await RequestAsync(await _client.SignInAsync(user), ManagerSetup.Password, newAddress);
        string secret = RecordingEmailSender.TokenIn(Emails.SentTo(newAddress).Single());

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        string[] stored = await db.EmailChangeTokens.AsNoTracking().Where(t => t.UserId == user.Id).Select(t => t.TokenHash).ToArrayAsync(Ct);

        stored.Should().ContainSingle().Which.Should().Be(_secrets.Hash(secret)).And.NotBe(secret);
    }

    [Fact]
    public async Task A_wrong_password_is_403_and_nothing_is_stored_or_sent()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();

        HttpResponseMessage response = await RequestAsync(await _client.SignInAsync(user), "Senha-errada-999", newAddress);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.password.incorrect");
        Emails.SentTo(newAddress).Should().BeEmpty();
        Emails.SentTo(user.Email.Value).Should().BeEmpty();
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.EmailChangeTokens.CountAsync(t => t.UserId == user.Id, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Five_wrong_attempts_lock_the_account_with_423()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(user);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await RequestAsync(token, "Senha-errada-999", NewAddress());
        }

        HttpResponseMessage response = await RequestAsync(token, ManagerSetup.Password, NewAddress());

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await response.CodeAsync()).Should().Be(LoginErrors.LockedOut.Code);
    }

    [Fact]
    public async Task An_invalid_address_is_400_and_does_not_spend_an_attempt()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(user);

        for (int attempt = 0; attempt < 6; attempt++)
        {
            HttpResponseMessage rejected = await RequestAsync(token, "Senha-errada-999", "isto-nao-e-um-email");
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await rejected.CodeAsync()).Should().Be("identity.email.invalid");
        }

        (await RequestAsync(token, ManagerSetup.Password, NewAddress())).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task The_same_address_is_400_and_does_not_spend_an_attempt()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await RequestAsync(token, "Senha-errada-999", user.Email.Value.ToUpperInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.email.unchanged");
        (await RequestAsync(token, ManagerSetup.Password, NewAddress())).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task An_address_that_belongs_to_someone_else_gets_the_same_202_and_nothing_is_sent_or_stored()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        User other = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);

        HttpResponseMessage response = await RequestAsync(await _client.SignInAsync(user), ManagerSetup.Password, other.Email.Value);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Emails.SentTo(other.Email.Value).Should().BeEmpty();
        Emails.SentTo(user.Email.Value).Should().BeEmpty();
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.EmailChangeTokens.CountAsync(t => t.UserId == user.Id, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task Without_a_token_the_answer_is_401()
    {
        HttpResponseMessage response = await RequestAsync(null, ManagerSetup.Password, NewAddress());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Confirming_swaps_the_address_and_the_old_one_stops_working_for_login()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();
        await RequestAsync(await _client.SignInAsync(user), ManagerSetup.Password, newAddress);
        string secret = RecordingEmailSender.TokenIn(Emails.SentTo(newAddress).Single());

        HttpResponseMessage response = await ConfirmAsync(secret);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.LoginAsync(newAddress, ManagerSetup.Password)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.LoginAsync(user.Email.Value, ManagerSetup.Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Confirming_ends_every_session_and_tells_the_old_address()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();
        HttpResponseMessage login = await _client.LoginAsync(user.Email.Value, ManagerSetup.Password);
        string? cookie = login.RefreshCookieValue();
        string token = await login.AccessTokenAsync();
        await RequestAsync(token, ManagerSetup.Password, newAddress);

        await ConfirmAsync(RecordingEmailSender.TokenIn(Emails.SentTo(newAddress).Single()));

        (await _client.MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.RefreshAsync(cookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        EmailMessage notice = Emails.SentTo(user.Email.Value).Single(m => m.Subject == "O e-mail da sua conta no Urban foi alterado");
        notice.TextBody.Should().Contain(newAddress).And.Contain("https://app.test/esqueci-a-senha");
    }

    [Fact]
    public async Task The_link_works_only_once()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();
        await RequestAsync(await _client.SignInAsync(user), ManagerSetup.Password, newAddress);
        string secret = RecordingEmailSender.TokenIn(Emails.SentTo(newAddress).Single());
        (await ConfirmAsync(secret)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage second = await ConfirmAsync(secret);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.CodeAsync()).Should().Be("identity.email_change.invalid");
    }

    [Fact]
    public async Task An_unknown_link_is_400_invalid()
    {
        HttpResponseMessage response = await ConfirmAsync("segredo-que-nunca-foi-emitido");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.email_change.invalid");
    }

    [Fact]
    public async Task An_expired_link_is_400_expired_and_changes_nothing()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();
        GeneratedSecret secret = _secrets.Generate();
        await using (IdentityDbContext db = fixture.Postgres.CreateContext())
        {
            db.EmailChangeTokens.Add(EmailChangeToken.Issue(user.Id, Email.Create(newAddress).Value, secret.Hash, DateTimeOffset.UtcNow.AddHours(-25)));
            await db.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await ConfirmAsync(secret.Value);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.email_change.expired");
        (await _client.LoginAsync(user.Email.Value, ManagerSetup.Password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task If_the_address_was_taken_meanwhile_confirming_fails_with_the_generic_answer_and_changes_nothing()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string newAddress = NewAddress();
        await RequestAsync(await _client.SignInAsync(user), ManagerSetup.Password, newAddress);
        string secret = RecordingEmailSender.TokenIn(Emails.SentTo(newAddress).Single());
        await using (IdentityDbContext db = fixture.Postgres.CreateContext())
        {
            db.Users.Add(UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, newAddress));
            await db.SaveChangesAsync(Ct);
        }

        HttpResponseMessage response = await ConfirmAsync(secret);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be("identity.email_change.invalid");
        (await _client.LoginAsync(user.Email.Value, ManagerSetup.Password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string NewAddress() => $"{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> RequestAsync(string? token, string password, string newEmail) =>
        _client.SendJsonAsync(HttpMethod.Post, ManagerSetup.ChangeEmailRoute, token, new { password, newEmail });

    private Task<HttpResponseMessage> ConfirmAsync(string token) =>
        _client.SendJsonAsync(HttpMethod.Post, ManagerSetup.ConfirmEmailChangeRoute, null, new { token });
}
