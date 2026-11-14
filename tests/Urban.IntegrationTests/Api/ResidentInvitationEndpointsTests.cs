using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Condominium;

namespace Urban.IntegrationTests.Api;

/// <summary>O convite do morador, de ponta a ponta: cria a conta pendente, manda o e-mail, o morador define a senha e entra.</summary>
[Collection(ApiCollection.Name)]
public sealed class ResidentInvitationEndpointsTests(ApiFixture fixture) : IDisposable
{
    private const string Residents = "/api/residents";
    private const string Units = "/api/units";
    private const string Subject = "Você foi convidado para o Urban";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly HttpClient _client = fixture.Factory.CreateClient(new() { HandleCookies = false });

    public void Dispose() => _client.Dispose();

    private static string InvitationRoute(Guid residentId) => $"{Residents}/{residentId}/invitation";

    // ---- Acesso ----

    [Fact]
    public async Task The_route_requires_a_token()
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, InvitationRoute(Guid.NewGuid()), null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Resident)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Only_the_manager_can_invite(UserRole role)
    {
        User user = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password, role: role);
        string token = await _client.SignInAsync(user);

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Post, InvitationRoute(Guid.NewGuid()), token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.CodeAsync()).Should().Be("identity.user.not_manager");
    }

    // ---- Convidar ----

    [Fact]
    public async Task Inviting_answers_202_creates_a_pending_resident_account_and_sends_the_email_with_a_72_hour_link()
    {
        (ManagerSession manager, Guid residentId, string email) = await SeedResidentAsync();

        HttpResponseMessage response = await Invite(manager, residentId);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        User account = await ReadAccountAsync(email);
        account.Role.Should().Be(UserRole.Resident);
        account.Status.Should().Be(AccountStatus.PendingVerification);
        account.CondominiumId.Should().Be(manager.CondominiumId);
        EmailMessage sent = fixture.Factory.Emails.SentTo(email).Should().ContainSingle().Subject;
        sent.Subject.Should().Be(Subject);
        sent.TextBody.Should().Contain("Residencial Vila Verde").And.Contain("https://app.test/confirmar-email?token=").And.Contain("72 horas");
        EmailVerificationToken token = await ReadLatestTokenAsync(account.Id);
        (token.ExpiresAt - token.CreatedAt).Should().Be(TimeSpan.FromHours(72));
    }

    [Fact]
    public async Task After_the_invitation_the_resident_shows_hasAccount_in_the_read_and_in_the_list()
    {
        (ManagerSession manager, Guid residentId, _) = await SeedResidentAsync();
        (await ReadResidentAsync(manager, residentId)).GetProperty("hasAccount").GetBoolean().Should().BeFalse();

        await Invite(manager, residentId);

        (await ReadResidentAsync(manager, residentId)).GetProperty("hasAccount").GetBoolean().Should().BeTrue();
        HttpResponseMessage list = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}?status=all", manager.AccessToken);
        JsonElement item = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == residentId);
        item.GetProperty("hasAccount").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task The_resident_chooses_a_password_from_the_invitation_and_signs_in()
    {
        (ManagerSession manager, Guid residentId, string email) = await SeedResidentAsync();
        await Invite(manager, residentId);
        string link = RecordingEmailSender.TokenIn(fixture.Factory.Emails.SentTo(email).Single());

        HttpResponseMessage confirmed = await _client.PostAsJsonAsync(
            "/api/auth/confirm-email",
            new { token = link, password = ManagerSetup.Password, termsVersion = fixture.Factory.TermsVersion },
            Ct);
        HttpResponseMessage login = await _client.LoginAsync(email, ManagerSetup.Password);
        HttpResponseMessage me = await _client.MeAsync(await login.AccessTokenAsync());

        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent, await confirmed.Content.ReadAsStringAsync(Ct));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement body = await me.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("role").GetString().Should().Be("Resident");
        body.GetProperty("condominiumId").GetGuid().Should().Be(manager.CondominiumId);
    }

    [Fact]
    public async Task Inviting_again_right_away_answers_429_and_sends_no_second_email()
    {
        (ManagerSession manager, Guid residentId, string email) = await SeedResidentAsync();
        await Invite(manager, residentId);

        HttpResponseMessage again = await Invite(manager, residentId);

        again.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await again.CodeAsync()).Should().Be("identity.resident_access.resend_too_soon");
        fixture.Factory.Emails.SentTo(email).Should().ContainSingle();
    }

    [Fact]
    public async Task Resending_after_the_interval_sends_a_new_link_to_the_same_account()
    {
        (ManagerSession manager, Guid residentId, string email) = await SeedResidentAsync();
        await Invite(manager, residentId);
        User account = await ReadAccountAsync(email);
        await AgeTheLinksAsync(account.Id);

        HttpResponseMessage again = await Invite(manager, residentId);

        again.StatusCode.Should().Be(HttpStatusCode.Accepted, await again.Content.ReadAsStringAsync(Ct));
        EmailMessage[] sent = fixture.Factory.Emails.SentTo(email);
        sent.Should().HaveCount(2);
        RecordingEmailSender.TokenIn(sent[0]).Should().NotBe(RecordingEmailSender.TokenIn(sent[1]));
        (await ReadAccountAsync(email)).Id.Should().Be(account.Id);
    }

    [Fact]
    public async Task Resending_to_a_resident_who_already_chose_a_password_answers_409()
    {
        (ManagerSession manager, Guid residentId, string email) = await SeedResidentAsync();
        await Invite(manager, residentId);
        string link = RecordingEmailSender.TokenIn(fixture.Factory.Emails.SentTo(email).Single());
        await _client.PostAsJsonAsync("/api/auth/confirm-email", new { token = link, password = ManagerSetup.Password, termsVersion = fixture.Factory.TermsVersion }, Ct);

        HttpResponseMessage again = await Invite(manager, residentId);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.CodeAsync()).Should().Be("identity.resident_access.not_pending");
    }

    [Fact]
    public async Task A_resident_without_email_cannot_be_invited()
    {
        (ManagerSession manager, Guid residentId, _) = await SeedResidentAsync(email: null);

        HttpResponseMessage response = await Invite(manager, residentId);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.CodeAsync()).Should().Be("condominium.resident.email_required");
    }

    [Fact]
    public async Task An_email_that_already_has_an_account_answers_409_and_leaves_the_resident_without_access()
    {
        User existing = await fixture.Postgres.SeedUserWithPasswordAsync(ManagerSetup.Password);
        (ManagerSession manager, Guid residentId, _) = await SeedResidentAsync(email: existing.Email.Value);

        HttpResponseMessage response = await Invite(manager, residentId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("identity.resident_access.email_unavailable");
        (await ReadResidentAsync(manager, residentId)).GetProperty("hasAccount").GetBoolean().Should().BeFalse();
        fixture.Factory.Emails.SentTo(existing.Email.Value).Should().BeEmpty();
    }

    [Fact]
    public async Task A_resident_of_another_condominium_answers_404_like_one_that_does_not_exist()
    {
        (_, Guid residentId, string email) = await SeedResidentAsync();
        ManagerSession other = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await Invite(other, residentId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.CodeAsync()).Should().Be("condominium.resident.not_found");
        fixture.Factory.Emails.SentTo(email).Should().BeEmpty();
    }

    [Fact]
    public async Task An_unknown_resident_answers_404()
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);

        HttpResponseMessage response = await Invite(manager, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- O e-mail trava depois do convite ----

    [Fact]
    public async Task After_the_invitation_the_email_of_the_resident_cannot_be_changed()
    {
        (ManagerSession manager, Guid residentId, _) = await SeedResidentAsync();
        await Invite(manager, residentId);
        JsonElement resident = await ReadResidentAsync(manager, residentId);

        HttpResponseMessage response = await _client.SendJsonAsync(
            HttpMethod.Put,
            $"{Residents}/{residentId}",
            manager.AccessToken,
            EditBody(resident.GetProperty("version").GetString()!, "outro@exemplo.com"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.CodeAsync()).Should().Be("condominium.resident.email_locked");
    }

    [Fact]
    public async Task After_the_invitation_the_other_data_can_still_be_edited_with_the_same_email()
    {
        (ManagerSession manager, Guid residentId, string email) = await SeedResidentAsync();
        await Invite(manager, residentId);
        JsonElement resident = await ReadResidentAsync(manager, residentId);
        JsonObject body = EditBody(resident.GetProperty("version").GetString()!, email);
        body["profession"] = "Médica";

        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Put, $"{Residents}/{residentId}", manager.AccessToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hasAccount").GetBoolean().Should().BeTrue();
    }

    // ---- Apoio ----

    private Task<HttpResponseMessage> Invite(ManagerSession manager, Guid residentId) =>
        _client.SendJsonAsync(HttpMethod.Post, InvitationRoute(residentId), manager.AccessToken);

    private async Task<(ManagerSession Manager, Guid ResidentId, string Email)> SeedResidentAsync(string? email = "")
    {
        ManagerSession manager = await ManagerSetup.RegisterAsync(fixture, _client);
        string address = email == string.Empty ? $"morador-{Guid.NewGuid():N}@exemplo.com" : email!;
        HttpResponseMessage unit = await _client.SendJsonAsync(HttpMethod.Post, Units, manager.AccessToken, new JsonObject { ["number"] = "101", ["type"] = "apartment" });
        unit.StatusCode.Should().Be(HttpStatusCode.Created, await unit.Content.ReadAsStringAsync(Ct));
        Guid unitId = (await unit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        JsonObject body = ResidentBody(unitId, address);
        HttpResponseMessage created = await _client.SendJsonAsync(HttpMethod.Post, Residents, manager.AccessToken, body);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));

        return (manager, (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), address ?? string.Empty);
    }

    private async Task<JsonElement> ReadResidentAsync(ManagerSession manager, Guid residentId)
    {
        HttpResponseMessage response = await _client.SendJsonAsync(HttpMethod.Get, $"{Residents}/{residentId}", manager.AccessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<User> ReadAccountAsync(string email)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();

        return await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email.Create(email).Value, Ct);
    }

    private async Task<EmailVerificationToken> ReadLatestTokenAsync(Guid userId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();

        return await db.EmailVerificationTokens.AsNoTracking().Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).FirstAsync(Ct);
    }

    /// <summary>Faz os links da conta parecerem emitidos há 5 minutos, para o reenvio passar do intervalo de 1 minuto.</summary>
    private async Task AgeTheLinksAsync(Guid userId)
    {
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        await db.Database.ExecuteSqlAsync($"update identity.email_verification_token set created_at = created_at - interval '5 minutes' where user_id = {userId}", Ct);
    }

    private static JsonObject ResidentBody(Guid unitId, string? email) => new()
    {
        ["fullName"] = "Maria Souza",
        ["cpf"] = CondominiumBuilders.UniqueCpf().Value,
        ["birthDate"] = "1990-05-20",
        ["phone"] = "(47) 99876-5432",
        ["email"] = email,
        ["profession"] = "Engenheira",
        ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
        ["unitId"] = unitId,
        ["kind"] = "owner",
        ["startDate"] = "2024-03-01",
    };

    private static JsonObject EditBody(string version, string email) => new()
    {
        ["version"] = version,
        ["fullName"] = "Maria Souza",
        ["birthDate"] = "1990-05-20",
        ["phone"] = "(47) 99876-5432",
        ["email"] = email,
        ["profession"] = "Engenheira",
        ["emergencyContact"] = new JsonObject { ["name"] = "Carlos Souza", ["relationship"] = "Irmão", ["phone"] = "(47) 3333-4444" },
    };
}
