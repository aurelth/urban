using System.Net;
using FluentAssertions;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Users;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class ChangePasswordEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string NewPassword = "Outra-senha-456";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    private RecordingEmailSender Emails => fixture.Factory.Emails;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task The_right_current_password_changes_it_and_answers_204()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await ChangeAsync(token, ManagerSetup.Password, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.LoginAsync(user.Email.Value, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.LoginAsync(user.Email.Value, ManagerSetup.Password)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Saving_the_new_password_ends_every_session_of_the_user()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        HttpResponseMessage login = await _client.LoginAsync(user.Email.Value, ManagerSetup.Password);
        string? cookie = login.RefreshCookieValue();
        string token = await login.AccessTokenAsync();

        (await ChangeAsync(token, ManagerSetup.Password, NewPassword)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.RefreshAsync(cookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_security_notice_goes_to_the_current_address_and_never_carries_a_password()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);

        await ChangeAsync(await _client.SignInAsync(user), ManagerSetup.Password, NewPassword);

        EmailMessage message = Emails.SentTo(user.Email.Value).Should().ContainSingle().Subject;
        message.Subject.Should().Be("A senha da sua conta no Urban foi alterada");
        message.TextBody.Should().Contain("https://app.test/esqueci-a-senha").And.NotContain(NewPassword).And.NotContain(ManagerSetup.Password);
    }

    [Fact]
    public async Task A_wrong_current_password_is_403_and_the_password_stays()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string hashBefore = (await fixture.Postgres.ReadCredentialAsync(user.Id))!.PasswordHash;

        HttpResponseMessage response = await ChangeAsync(await _client.SignInAsync(user), "Senha-errada-999", NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.password.incorrect");
        (await fixture.Postgres.ReadCredentialAsync(user.Id))!.PasswordHash.Should().Be(hashBefore);
        Emails.SentTo(user.Email.Value).Should().BeEmpty();
    }

    [Fact]
    public async Task Five_wrong_attempts_lock_the_account_with_423()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(user);
        for (int attempt = 0; attempt < 5; attempt++)
        {
            await ChangeAsync(token, "Senha-errada-999", NewPassword);
        }

        HttpResponseMessage response = await ChangeAsync(token, ManagerSetup.Password, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        (await response.CodeAsync()).Should().Be(LoginErrors.LockedOut.Code);
    }

    [Theory]
    [InlineData("curta1", "identity.password.too_short")]
    [InlineData("somente-letras-aqui", "identity.password.needs_letter_and_digit")]
    public async Task A_weak_new_password_is_400_and_does_not_spend_an_attempt(string weak, string code)
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(user);

        for (int attempt = 0; attempt < 6; attempt++)
        {
            HttpResponseMessage rejected = await ChangeAsync(token, "Senha-errada-999", weak);
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await rejected.CodeAsync()).Should().Be(code);
        }

        (await ChangeAsync(token, ManagerSetup.Password, NewPassword)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_new_password_equal_to_the_current_one_is_400_and_does_not_spend_an_attempt()
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        string token = await _client.SignInAsync(user);

        for (int attempt = 0; attempt < 6; attempt++)
        {
            HttpResponseMessage rejected = await ChangeAsync(token, ManagerSetup.Password, ManagerSetup.Password);
            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await rejected.CodeAsync()).Should().Be("identity.password.unchanged");
        }

        (await ChangeAsync(token, ManagerSetup.Password, NewPassword)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Without_a_token_the_answer_is_401()
    {
        HttpResponseMessage response = await ChangeAsync(null, ManagerSetup.Password, NewPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> ChangeAsync(string? token, string current, string next) =>
        _client.SendJsonAsync(HttpMethod.Post, ManagerSetup.ChangePasswordRoute, token, new { currentPassword = current, newPassword = next });
}
