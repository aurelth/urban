using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Consent;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.Identity.Infrastructure.Tokens;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class RegistrationEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Password = "Senha-segura-123";

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    private readonly ISecretGenerator _secrets = new SecretGenerator();

    private RecordingEmailSender Emails => fixture.Factory.Emails;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Register_with_a_new_email_returns_202_and_creates_a_manager_waiting_for_approval()
    {
        string address = NewAddress();

        HttpResponseMessage response = await RegisterAsync(address);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email.Create(address).Value);
        user.Status.Should().Be(AccountStatus.PendingApproval);
        user.Role.Should().Be(UserRole.Manager);
        user.DisplayName!.Value.Should().Be("Ana Paula");
        user.RequestedCondominiumName!.Value.Should().Be("Residencial Vila Verde");
        (await db.UserCredentials.AnyAsync(c => c.UserId == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Register_sends_nothing_to_the_person_before_the_owner_approves()
    {
        string address = NewAddress();

        await RegisterAsync(address);

        Emails.SentTo(address).Should().BeEmpty();
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.EmailVerificationTokens.AnyAsync(t => db.Users.Any(u => u.Id == t.UserId && u.Email == Email.Create(address).Value))).Should().BeFalse();
    }

    [Fact]
    public async Task Register_twice_in_a_row_keeps_a_single_request_and_answers_the_same()
    {
        string address = NewAddress();

        HttpResponseMessage first = await RegisterAsync(address);
        HttpResponseMessage second = await RegisterAsync(address.ToUpperInvariant());

        second.StatusCode.Should().Be(first.StatusCode);
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.Users.CountAsync(u => u.Email == Email.Create(address).Value)).Should().Be(1);
        Emails.SentTo(address).Should().BeEmpty();
    }

    [Fact]
    public async Task Register_after_the_resend_interval_sends_a_new_link()
    {
        User user = await fixture.Postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-2));

        HttpResponseMessage response = await RegisterAsync(user.Email.Value);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Emails.SentTo(user.Email.Value).Should().ContainSingle();
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.EmailVerificationTokens.CountAsync(t => t.UserId == user.Id)).Should().Be(2);
    }

    [Theory]
    [InlineData(AccountStatus.Active)]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.PendingApproval)]
    [InlineData(AccountStatus.Rejected)]
    public async Task Register_with_an_email_that_already_has_an_account_that_cannot_get_a_link_answers_the_same_and_sends_nothing(AccountStatus status)
    {
        User user = await fixture.Postgres.SeedUserAsync(status, UserRole.Manager);

        HttpResponseMessage response = await RegisterAsync(user.Email.Value);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        Emails.SentTo(user.Email.Value).Should().BeEmpty();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"email":""}""")]
    [InlineData("""{"email":"sem-arroba"}""")]
    public async Task Register_with_an_invalid_email_returns_400_with_a_stable_code(string json)
    {
        HttpResponseMessage response = await _client.PostAsync("/api/auth/register", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(Email.Invalid.Code);
    }

    [Theory]
    [InlineData("""{"email":"ana@example.com"}""")]
    [InlineData("""{"email":"ana@example.com","name":"A","condominiumName":"Vila Verde"}""")]
    [InlineData("""{"email":"ana@example.com","name":"Ana 123","condominiumName":"Vila Verde"}""")]
    public async Task Register_with_an_invalid_name_returns_400_with_a_stable_code(string json)
    {
        HttpResponseMessage response = await _client.PostAsync("/api/auth/register", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(DisplayName.Invalid.Code);
    }

    [Theory]
    [InlineData("""{"email":"ana@example.com","name":"Ana"}""")]
    [InlineData("""{"email":"ana@example.com","name":"Ana","condominiumName":"X"}""")]
    public async Task Register_with_an_invalid_condominium_name_returns_400_with_a_stable_code(string json)
    {
        HttpResponseMessage response = await _client.PostAsync("/api/auth/register", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(RequestedCondominiumName.Invalid.Code);
    }

    [Fact]
    public async Task Confirm_activates_the_account_and_the_chosen_password_works_to_log_in()
    {
        (string address, string token) = await SeedApprovedAsync();

        HttpResponseMessage confirmed = await ConfirmAsync(token, Password);
        HttpResponseMessage login = await _client.LoginAsync(address, Password);

        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_before_confirming_is_still_refused()
    {
        string address = NewAddress();
        await RegisterAsync(address);

        HttpResponseMessage login = await _client.LoginAsync(address, Password);

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Confirm_twice_with_the_same_link_fails_the_second_time()
    {
        string token = await RegisterAndGetTokenAsync();
        await ConfirmAsync(token, Password);

        HttpResponseMessage second = await ConfirmAsync(token, "Outra-senha-456");

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await second.CodeAsync()).Should().Be(EmailVerificationTokenErrors.Invalid.Code);
    }

    [Fact]
    public async Task Confirm_with_a_weak_password_is_refused_without_spending_the_link()
    {
        (_, string token) = await SeedApprovedAsync();

        HttpResponseMessage weak = await ConfirmAsync(token, "curta1");
        HttpResponseMessage retry = await ConfirmAsync(token, Password);

        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await weak.CodeAsync()).Should().Be(PasswordPolicy.TooShort.Code);
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Confirm_records_the_accepted_terms_version_for_the_new_user()
    {
        (string address, string token) = await SeedApprovedAsync();
        DateTimeOffset before = DateTimeOffset.UtcNow;

        await ConfirmAsync(token, Password);

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email.Create(address).Value);
        UserConsent consent = await db.UserConsents.AsNoTracking().SingleAsync(c => c.UserId == user.Id);
        consent.Version.Should().Be(fixture.Factory.TermsVersion);
        consent.AcceptedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Confirm_without_the_terms_version_is_refused_without_spending_the_link(string? missing)
    {
        string token = await RegisterAndGetTokenAsync();

        HttpResponseMessage refused = await ConfirmWithTermsAsync(token, Password, missing);
        HttpResponseMessage retry = await ConfirmAsync(token, Password);

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.CodeAsync()).Should().Be(ConsentErrors.Required.Code);
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Confirm_with_an_outdated_terms_version_is_refused_and_leaves_the_account_pending()
    {
        (string address, string token) = await SeedApprovedAsync();

        HttpResponseMessage refused = await ConfirmWithTermsAsync(token, Password, "2020-01-01");
        HttpResponseMessage retry = await ConfirmAsync(token, Password);

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.CodeAsync()).Should().Be(ConsentErrors.Outdated.Code);
        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email.Create(address).Value);
        (await db.UserConsents.CountAsync(c => c.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Confirm_with_an_unknown_link_returns_400()
    {
        HttpResponseMessage response = await ConfirmAsync("link-que-nunca-existiu", Password);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(EmailVerificationTokenErrors.Invalid.Code);
    }

    [Fact]
    public async Task Confirm_without_a_body_returns_400_for_the_password()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/auth/confirm-email", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(PasswordPolicy.TooShort.Code);
    }

    [Fact]
    public async Task Confirm_with_an_expired_link_returns_400_and_leaves_the_account_pending()
    {
        User user = await fixture.Postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        string secret = await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddHours(-25));

        HttpResponseMessage response = await ConfirmAsync(secret, Password);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.CodeAsync()).Should().Be(EmailVerificationTokenErrors.Expired.Code);
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Status.Should().Be(AccountStatus.PendingVerification);
    }

    [Fact]
    public async Task Confirm_with_a_link_of_an_account_that_is_no_longer_pending_returns_400()
    {
        User user = await fixture.Postgres.SeedUserAsync(AccountStatus.Suspended, UserRole.Manager);
        string secret = await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        HttpResponseMessage response = await ConfirmAsync(secret, Password);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fixture.Postgres.ReadCredentialAsync(user.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Both_links_work_after_a_resend_but_only_the_first_confirmation_wins()
    {
        User user = await fixture.Postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        string oldSecret = await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-2));
        await RegisterAsync(user.Email.Value);
        string newSecret = RecordingEmailSender.TokenIn(Emails.SentTo(user.Email.Value).Single());

        HttpResponseMessage first = await ConfirmAsync(newSecret, Password);
        HttpResponseMessage second = await ConfirmAsync(oldSecret, "Outra-senha-456");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.LoginAsync(user.Email.Value, Password)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string NewAddress() => $"{Guid.NewGuid():N}@example.com";

    private Task<HttpResponseMessage> RegisterAsync(string email) =>
        _client.PostAsJsonAsync("/api/auth/register", new { email, name = "Ana Paula", condominiumName = "Residencial Vila Verde" });

    private Task<HttpResponseMessage> ConfirmAsync(string token, string password) =>
        ConfirmWithTermsAsync(token, password, fixture.Factory.TermsVersion);

    private Task<HttpResponseMessage> ConfirmWithTermsAsync(string token, string password, string? termsVersion) =>
        _client.PostAsJsonAsync("/api/auth/confirm-email", new { token, password, termsVersion });

    /// <summary>Uma pessoa que o dono já aprovou: conta pendente de confirmação, com um link válido e o segredo dele.</summary>
    private async Task<(string Address, string Token)> SeedApprovedAsync()
    {
        User user = await fixture.Postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        string secret = await SeedLinkAsync(user.Id, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        return (user.Email.Value, secret);
    }

    private async Task<string> RegisterAndGetTokenAsync() => (await SeedApprovedAsync()).Token;

    private async Task<string> SeedLinkAsync(Guid userId, DateTimeOffset issuedAt)
    {
        GeneratedSecret secret = _secrets.Generate();
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        db.EmailVerificationTokens.Add(EmailVerificationToken.Issue(userId, secret.Hash, issuedAt));
        await db.SaveChangesAsync();

        return secret.Value;
    }
}