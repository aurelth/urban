using FluentAssertions;
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

    private static SpyPasswordHasher Spy() => new(PostgresFixture.Hasher);

    private async Task<Result<User>> CheckAsync(string email, string password, SpyPasswordHasher? hasher = null)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        UserAccounts accounts = new(db, hasher ?? PostgresFixture.Hasher, _clock);

        return await accounts.CheckCredentialsAsync(Email.Create(email).Value, password, Ct);
    }

    private async Task FailAsync(User user, int times)
    {
        for (int attempt = 0; attempt < times; attempt++)
        {
            await CheckAsync(user.Email.Value, WrongPassword);
        }
    }
}