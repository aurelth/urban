using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

[Collection(ApiCollection.Name)]
public sealed class SuperAdminBootstrapTests(ApiFixture fixture)
{
    private const string Password = "Senha-segura-123";

    [Fact]
    public async Task Starting_with_a_configured_email_creates_a_pending_super_admin_without_condominium_or_password()
    {
        string address = NewAddress();

        await using ApiFactory factory = Start(address);

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email.Create(address).Value);
        user.Role.Should().Be(UserRole.SuperAdmin);
        user.Status.Should().Be(AccountStatus.PendingVerification);
        user.CondominiumId.Should().BeNull();
        (await db.UserCredentials.AnyAsync(c => c.UserId == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Starting_sends_one_email_with_a_link_to_the_confirmation_screen()
    {
        string address = NewAddress();

        await using ApiFactory factory = Start(address);

        EmailMessage message = factory.Emails.SentTo(address).Should().ContainSingle().Subject;
        message.TextBody.Should().Contain("https://app.test/confirmar-email?token=");
    }

    [Fact]
    public async Task Starting_again_keeps_a_single_account_and_does_not_send_another_link_right_away()
    {
        string address = NewAddress();
        await using ApiFactory first = Start(address);

        await using ApiFactory second = Start(address);

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        (await db.Users.CountAsync(u => u.Email == Email.Create(address).Value)).Should().Be(1);
        first.Emails.SentTo(address).Should().ContainSingle();
        second.Emails.SentTo(address).Should().BeEmpty();
    }

    [Fact]
    public async Task Starting_while_the_account_is_still_pending_and_has_no_recent_link_sends_a_new_one()
    {
        User pending = await fixture.Postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.SuperAdmin);

        await using ApiFactory factory = Start(pending.Email.Value);

        factory.Emails.SentTo(pending.Email.Value).Should().ContainSingle();
    }

    [Fact]
    public async Task Starting_with_an_active_super_admin_sends_nothing()
    {
        User active = await fixture.Postgres.SeedUserAsync(AccountStatus.Active, UserRole.SuperAdmin);

        await using ApiFactory factory = Start(active.Email.Value);

        factory.Emails.SentTo(active.Email.Value).Should().BeEmpty();
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Resident)]
    public async Task Starting_with_an_email_that_belongs_to_another_role_starts_anyway_and_changes_nothing(UserRole role)
    {
        User other = await fixture.Postgres.SeedUserAsync(AccountStatus.PendingVerification, role);

        await using ApiFactory factory = Start(other.Email.Value);

        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == other.Id);
        stored.Role.Should().Be(role);
        factory.Emails.SentTo(other.Email.Value).Should().BeEmpty();
    }

    [Fact]
    public async Task The_link_from_the_bootstrap_activates_the_account_as_super_admin_with_the_chosen_password()
    {
        string address = NewAddress();
        await using ApiFactory factory = Start(address);
        string token = RecordingEmailSender.TokenIn(factory.Emails.SentTo(address).Single());
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage confirmed = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token, password = Password, termsVersion = factory.TermsVersion });

        confirmed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using IdentityDbContext db = fixture.Postgres.CreateContext();
        User user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == Email.Create(address).Value);
        user.Status.Should().Be(AccountStatus.Active);
        user.Role.Should().Be(UserRole.SuperAdmin);
        (await db.UserCredentials.AnyAsync(c => c.UserId == user.Id)).Should().BeTrue();
        (await db.UserConsents.SingleAsync(c => c.UserId == user.Id)).Version.Should().Be(factory.TermsVersion);
    }

    [Fact]
    public void Refuses_to_start_when_the_configured_email_is_invalid()
    {
        using ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = ApiFactory.UnusedRedis,
            SuperAdminEmail = "sem-arroba",
        };

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*SuperAdmin:Email*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Starting_without_a_configured_email_starts_normally(string setting)
    {
        using ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = ApiFactory.UnusedRedis,
            SuperAdminEmail = setting,
        };

        Action start = () => factory.CreateClient().Dispose();

        start.Should().NotThrow();
    }

    private static string NewAddress() => $"{Guid.NewGuid():N}@example.com";

    private ApiFactory Start(string address)
    {
        ApiFactory factory = new()
        {
            PostgresConnectionString = fixture.Postgres.ConnectionString,
            RedisConnectionString = ApiFactory.UnusedRedis,
            SuperAdminEmail = address,
        };
        factory.CreateClient().Dispose();
        return factory;
    }
}