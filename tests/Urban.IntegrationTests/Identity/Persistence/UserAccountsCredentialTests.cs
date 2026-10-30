using FluentAssertions;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;
using Urban.TestSupport.Time;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class UserAccountsCredentialTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly FakeClock _clock = new(UserBuilder.Now);

    [Fact]
    public async Task FindCredential_returns_the_credential_of_the_user_or_null()
    {
        User withPassword = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        User withoutPassword = await postgres.SeedUserAsync();

        UserCredential? found = await FindAsync(withPassword.Id);
        UserCredential? missing = await FindAsync(withoutPassword.Id);

        found!.UserId.Should().Be(withPassword.Id);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task SaveCredential_stores_the_new_hash_and_clears_the_failures()
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        await using IdentityDbContext db = postgres.CreateContext();
        UserAccounts accounts = new(db, PostgresFixture.Hasher, _clock);
        UserCredential credential = (await accounts.FindCredentialAsync(user.Id, Ct))!;
        credential.RegisterFailure(UserBuilder.Now);
        credential.ChangePassword("novo-hash");

        await accounts.SaveCredentialAsync(credential, Ct);

        UserCredential stored = (await postgres.ReadCredentialAsync(user.Id))!;
        stored.PasswordHash.Should().Be("novo-hash");
        stored.FailedAttempts.Should().Be(0);
    }

    private async Task<UserCredential?> FindAsync(Guid userId)
    {
        await using IdentityDbContext db = postgres.CreateContext();

        return await new UserAccounts(db, PostgresFixture.Hasher, _clock).FindCredentialAsync(userId, Ct);
    }
}
