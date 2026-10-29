using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Domain.Access;
using Urban.Identity.Application.Authentication.Login;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Identity;
using Urban.TestSupport.Time;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class UserAccountsTests(PostgresFixture postgres)
{
    private const string Password = "Senha-correta-1";
    private const string WrongPassword = "Senha-errada-1";

    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeClock _clock = new(UserBuilder.Now);

    [Fact]
    public async Task The_right_password_returns_the_user()
    {
        User user = await postgres.SeedUserWithPasswordAsync(Password);

        Result<User> result = await CheckAsync(user.Email.Value, Password);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task An_unknown_email_fails_with_the_same_error_as_a_wrong_password()
    {
        Result<User> unknown = await CheckAsync($"{Guid.NewGuid():N}@example.com", Password);

        User user = await postgres.SeedUserWithPasswordAsync(Password);
        Result<User> wrong = await CheckAsync(user.Email.Value, WrongPassword);

        unknown.Error.Should().Be(LoginErrors.InvalidCredentials);
        wrong.Error.Should().Be(LoginErrors.InvalidCredentials);
    }

    [Fact]
    public async Task An_unknown_email_still_spends_the_time_of_a_real_check()
    {
        SpyPasswordHasher hasher = Spy();

        await CheckAsync($"{Guid.NewGuid():N}@example.com", Password, hasher);

        hasher.DummyCalls.Should().Be(1);
        hasher.VerifyCalls.Should().Be(0);
    }

    [Fact]
    public async Task A_wrong_password_counts_one_failure()
    {
        User user = await postgres.SeedUserWithPasswordAsync(Password);

        await CheckAsync(user.Email.Value, WrongPassword);

        (await postgres.ReadCredentialAsync(user.Id))!.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task The_fifth_wrong_password_locks_the_account_for_fifteen_minutes()
    {
        User user = await postgres.SeedUserWithPasswordAsync(Password);
        await FailAsync(user, 4);

        Result<User> fifth = await CheckAsync(user.Email.Value, WrongPassword);

        fifth.Error.Should().Be(LoginErrors.LockedOut);
        UserCredential credential = (await postgres.ReadCredentialAsync(user.Id))!;
        credential.LockoutEnd.Should().Be(UserBuilder.Now.AddMinutes(15));
        credential.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task While_locked_even_the_right_password_is_refused_without_being_checked()
    {
        User user = await postgres.SeedUserWithPasswordAsync(Password);
        await FailAsync(user, 5);
        SpyPasswordHasher hasher = Spy();

        Result<User> result = await CheckAsync(user.Email.Value, Password, hasher);

        result.Error.Should().Be(LoginErrors.LockedOut);
        hasher.VerifyCalls.Should().Be(0);
    }

    [Fact]
    public async Task After_the_lockout_ends_the_right_password_works_again_and_clears_the_lockout()
    {
        User user = await postgres.SeedUserWithPasswordAsync(Password);
        await FailAsync(user, 5);
        _clock.Advance(UserCredential.LockoutDuration);

        Result<User> result = await CheckAsync(user.Email.Value, Password);

        result.IsSuccess.Should().BeTrue();
        (await postgres.ReadCredentialAsync(user.Id))!.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public async Task The_right_password_clears_the_failures_counted_so_far()
    {
        User user = await postgres.SeedUserWithPasswordAsync(Password);
        await FailAsync(user, 4);

        await CheckAsync(user.Email.Value, Password);

        (await postgres.ReadCredentialAsync(user.Id))!.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task A_failure_recorded_at_the_same_time_by_another_request_is_not_lost()
    {
        User user = await postgres.SeedUserWithPasswordAsync(Password);
        SpyPasswordHasher hasher = Spy();
        hasher.ConcurrentWork = () => CheckAsync(user.Email.Value, WrongPassword);

        await CheckAsync(user.Email.Value, WrongPassword, hasher);

        (await postgres.ReadCredentialAsync(user.Id))!.FailedAttempts.Should().Be(2);
    }

    [Fact]
    public async Task FindById_returns_the_user()
    {
        User user = await postgres.SeedUserAsync();

        await using IdentityDbContext db = postgres.CreateContext();
        User? found = await new UserAccounts(db, PostgresFixture.Hasher, _clock).FindByIdAsync(user.Id, Ct);

        found.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task FindById_returns_null_for_an_unknown_id()
    {
        await using IdentityDbContext db = postgres.CreateContext();

        User? found = await new UserAccounts(db, PostgresFixture.Hasher, _clock).FindByIdAsync(Guid.CreateVersion7(), Ct);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Saving_an_enrollment_stores_the_condominium_and_the_display_name_of_a_user_read_without_tracking()
    {
        User seeded = await postgres.SeedManagerWithoutCondominiumAsync();
        Guid condominiumId = Guid.CreateVersion7();
        User manager = (await FindAsync(seeded.Id))!;
        manager.AssignCondominium(condominiumId, DisplayName.Create("Ana").Value).IsSuccess.Should().BeTrue();

        await SaveEnrollmentAsync(manager);

        User? stored = await FindAsync(seeded.Id);
        stored!.CondominiumId.Should().Be(condominiumId);
        stored.DisplayName!.Value.Should().Be("Ana");
    }

    [Fact]
    public async Task Saving_an_enrollment_does_not_overwrite_what_changed_in_the_account_meanwhile()
    {
        User seeded = await postgres.SeedManagerWithoutCondominiumAsync();
        User stale = (await FindAsync(seeded.Id))!;
        await SuspendAsync(seeded.Id);
        stale.AssignCondominium(Guid.CreateVersion7(), DisplayName.Create("Ana").Value);

        await SaveEnrollmentAsync(stale);

        User? stored = await FindAsync(seeded.Id);
        stored!.Status.Should().Be(AccountStatus.Suspended);
        stored.DisplayName.Should().NotBeNull();
    }

    [Fact]
    public async Task Saving_an_enrollment_does_not_touch_other_users()
    {
        User other = await postgres.SeedManagerWithoutCondominiumAsync();
        User seeded = await postgres.SeedManagerWithoutCondominiumAsync();
        User manager = (await FindAsync(seeded.Id))!;
        manager.AssignCondominium(Guid.CreateVersion7(), DisplayName.Create("Ana").Value);

        await SaveEnrollmentAsync(manager);

        User? untouched = await FindAsync(other.Id);
        untouched!.CondominiumId.Should().BeNull();
        untouched.DisplayName.Should().BeNull();
    }

    private static SpyPasswordHasher Spy() => new(PostgresFixture.Hasher);

    private async Task<Result<User>> CheckAsync(string email, string password, SpyPasswordHasher? hasher = null)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        UserAccounts accounts = new(db, hasher ?? PostgresFixture.Hasher, _clock);

        return await accounts.CheckCredentialsAsync(Email.Create(email).Value, password, Ct);
    }

    private async Task<User?> FindAsync(Guid id)
    {
        await using IdentityDbContext db = postgres.CreateContext();

        return await new UserAccounts(db, PostgresFixture.Hasher, _clock).FindByIdAsync(id, Ct);
    }

    private async Task SaveEnrollmentAsync(User user)
    {
        await using IdentityDbContext db = postgres.CreateContext();

        await new UserAccounts(db, PostgresFixture.Hasher, _clock).SaveEnrollmentAsync(user, Ct);
    }

    private async Task SuspendAsync(Guid id)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        User tracked = await db.Users.SingleAsync(u => u.Id == id, Ct);
        tracked.Suspend(UserBuilder.ActorId, "Motivo de teste", UserBuilder.Now).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync(Ct);
    }

    private async Task FailAsync(User user, int times)
    {
        for (int attempt = 0; attempt < times; attempt++)
        {
            await CheckAsync(user.Email.Value, WrongPassword);
        }
    }
}