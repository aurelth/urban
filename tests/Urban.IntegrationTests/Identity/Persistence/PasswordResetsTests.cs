using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class PasswordResetsTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Add_rejects_a_link_whose_hash_already_exists()
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        string hash = NewHash();
        await RunAsync(r => r.AddAsync(PasswordResetToken.Issue(user.Id, hash, Now), Ct));

        Func<Task> addDuplicate = () => RunAsync(r => r.AddAsync(PasswordResetToken.Issue(user.Id, hash, Now), Ct));

        await addDuplicate.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Add_rejects_a_link_of_a_user_that_does_not_exist()
    {
        Func<Task> add = () => RunAsync(r => r.AddAsync(PasswordResetToken.Issue(Guid.CreateVersion7(), NewHash(), Now), Ct));

        await add.ShouldBeRejectedByAsync(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task FindByHash_returns_the_stored_link_or_null()
    {
        (PasswordResetToken token, _) = await SeedLinkAsync();

        PasswordResetToken? found = await RunAsync(r => r.FindByHashAsync(token.TokenHash, Ct));
        PasswordResetToken? unknown = await RunAsync(r => r.FindByHashAsync(NewHash(), Ct));

        found.Should().BeEquivalentTo(token);
        unknown.Should().BeNull();
    }

    [Fact]
    public async Task FindLatestOfUser_returns_the_most_recent_link()
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        PasswordResetToken older = PasswordResetToken.Issue(user.Id, NewHash(), Now.AddMinutes(-10));
        PasswordResetToken newer = PasswordResetToken.Issue(user.Id, NewHash(), Now.AddMinutes(-2));
        await RunAsync(r => r.AddAsync(newer, Ct));
        await RunAsync(r => r.AddAsync(older, Ct));

        PasswordResetToken? latest = await RunAsync(r => r.FindLatestOfUserAsync(user.Id, Ct));

        latest!.Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task FindLatestOfUser_returns_null_when_the_user_has_no_link()
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");

        PasswordResetToken? latest = await RunAsync(r => r.FindLatestOfUserAsync(user.Id, Ct));

        latest.Should().BeNull();
    }

    [Fact]
    public async Task FindLatestOfUser_ignores_the_links_of_other_users()
    {
        (_, User owner) = await SeedLinkAsync();
        User other = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");

        PasswordResetToken? latest = await RunAsync(r => r.FindLatestOfUserAsync(other.Id, Ct));

        latest.Should().BeNull();
        (await RunAsync(r => r.FindLatestOfUserAsync(owner.Id, Ct))).Should().NotBeNull();
    }

    [Fact]
    public async Task FindCredentialOfUser_returns_the_credential_or_null()
    {
        User withPassword = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        User withoutPassword = await postgres.SeedUserAsync();

        UserCredential? found = await RunAsync(r => r.FindCredentialOfUserAsync(withPassword.Id, Ct));
        UserCredential? missing = await RunAsync(r => r.FindCredentialOfUserAsync(withoutPassword.Id, Ct));

        found!.UserId.Should().Be(withPassword.Id);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Reset_saves_the_used_link_the_new_hash_and_the_unlock_together()
    {
        (PasswordResetToken token, User user) = await SeedLinkAsync();
        await LockAsync(user.Id);

        bool reset = await ResetAsync(token.TokenHash, "novo-hash");

        reset.Should().BeTrue();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id)).UsedAt.Should().Be(Now);
        UserCredential credential = (await postgres.ReadCredentialAsync(user.Id))!;
        credential.PasswordHash.Should().Be("novo-hash");
        credential.IsLockedOut(Now).Should().BeFalse();
        credential.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Reset_lets_only_one_of_two_simultaneous_requests_win()
    {
        (PasswordResetToken token, User user) = await SeedLinkAsync();
        await using IdentityDbContext firstDb = postgres.CreateContext();
        await using IdentityDbContext secondDb = postgres.CreateContext();
        PasswordResets first = new(firstDb);
        PasswordResets second = new(secondDb);
        (PasswordResetToken firstToken, UserCredential firstCredential) = await LoadAsync(first, token.TokenHash, user.Id);
        (PasswordResetToken secondToken, UserCredential secondCredential) = await LoadAsync(second, token.TokenHash, user.Id);
        firstToken.Consume(Now);
        secondToken.Consume(Now);
        firstCredential.ChangePassword("first");
        secondCredential.ChangePassword("second");

        bool firstWon = await first.ResetAsync(firstToken, firstCredential, Ct);
        bool secondWon = await second.ResetAsync(secondToken, secondCredential, Ct);

        firstWon.Should().BeTrue();
        secondWon.Should().BeFalse();
        (await postgres.ReadCredentialAsync(user.Id))!.PasswordHash.Should().Be("first");
    }

    [Fact]
    public async Task Reset_returns_false_and_saves_nothing_when_the_link_was_used_in_between()
    {
        (PasswordResetToken token, User owner) = await SeedLinkAsync();
        User other = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        string otherHash = (await postgres.ReadCredentialAsync(other.Id))!.PasswordHash;
        await using IdentityDbContext firstDb = postgres.CreateContext();
        await using IdentityDbContext secondDb = postgres.CreateContext();
        PasswordResets first = new(firstDb);
        PasswordResets second = new(secondDb);
        (PasswordResetToken firstToken, UserCredential ownerCredential) = await LoadAsync(first, token.TokenHash, owner.Id);
        PasswordResetToken secondToken = (await second.FindByHashAsync(token.TokenHash, Ct))!;
        UserCredential unrelated = (await second.FindCredentialOfUserAsync(other.Id, Ct))!;
        firstToken.Consume(Now);
        ownerCredential.ChangePassword("first");
        await first.ResetAsync(firstToken, ownerCredential, Ct);
        secondToken.Consume(Now);
        unrelated.ChangePassword("second");

        // Nada mais conflitaria (outra conta, outra credencial): só o link já usado pode barrar esta gravação.
        bool reset = await second.ResetAsync(secondToken, unrelated, Ct);

        reset.Should().BeFalse();
        (await postgres.ReadCredentialAsync(other.Id))!.PasswordHash.Should().Be(otherHash);
    }

    [Fact]
    public async Task Reset_refuses_a_link_that_was_not_marked_as_used()
    {
        (PasswordResetToken token, User user) = await SeedLinkAsync();
        await using IdentityDbContext db = postgres.CreateContext();
        PasswordResets resets = new(db);
        (PasswordResetToken loadedToken, UserCredential credential) = await LoadAsync(resets, token.TokenHash, user.Id);
        credential.ChangePassword("novo-hash");

        Func<Task> reset = () => resets.ResetAsync(loadedToken, credential, Ct);

        await reset.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Reset_also_uses_up_the_other_links_still_open_of_the_same_user()
    {
        (PasswordResetToken token, User user) = await SeedLinkAsync();
        PasswordResetToken sibling = PasswordResetToken.Issue(user.Id, NewHash(), Now.AddMinutes(-3));
        PasswordResetToken expired = PasswordResetToken.Issue(user.Id, NewHash(), Now.AddHours(-3));
        PasswordResetToken othersLink = (await SeedLinkAsync()).Token;
        await RunAsync(r => r.AddAsync(sibling, Ct));
        await RunAsync(r => r.AddAsync(expired, Ct));

        await ResetAsync(token.TokenHash, "novo-hash");

        await using IdentityDbContext db = postgres.CreateContext();
        (await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.Id == sibling.Id)).UsedAt.Should().Be(Now);
        (await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.Id == expired.Id)).UsedAt.Should().BeNull();
        (await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.Id == othersLink.Id)).UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task Reset_returns_false_and_keeps_the_link_unused_when_the_credential_changed_in_between()
    {
        (PasswordResetToken token, User user) = await SeedLinkAsync();
        await using IdentityDbContext resetDb = postgres.CreateContext();
        PasswordResets resets = new(resetDb);
        (PasswordResetToken loadedToken, UserCredential loadedCredential) = await LoadAsync(resets, token.TokenHash, user.Id);
        await LockAsync(user.Id);
        loadedToken.Consume(Now);
        loadedCredential.ChangePassword("novo-hash");

        bool reset = await resets.ResetAsync(loadedToken, loadedCredential, Ct);

        reset.Should().BeFalse();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id)).UsedAt.Should().BeNull();
        (await postgres.ReadCredentialAsync(user.Id))!.PasswordHash.Should().NotBe("novo-hash");
    }

    private static string NewHash() => Guid.NewGuid().ToString("N");

    private async Task<(PasswordResetToken Token, User User)> SeedLinkAsync()
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        PasswordResetToken token = PasswordResetToken.Issue(user.Id, NewHash(), Now.AddMinutes(-5));
        await RunAsync(r => r.AddAsync(token, Ct));
        return (token, user);
    }

    private async Task LockAsync(Guid userId)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        UserCredential credential = await db.UserCredentials.SingleAsync(c => c.UserId == userId, Ct);
        for (int i = 0; i < UserCredential.MaxFailedAttempts; i++)
        {
            credential.RegisterFailure(Now.AddMinutes(-1));
        }

        await db.SaveChangesAsync(Ct);
    }

    private static async Task<(PasswordResetToken Token, UserCredential Credential)> LoadAsync(PasswordResets resets, string tokenHash, Guid userId) =>
        ((await resets.FindByHashAsync(tokenHash, Ct))!, (await resets.FindCredentialOfUserAsync(userId, Ct))!);

    private async Task<bool> ResetAsync(string tokenHash, string newHash)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        PasswordResets resets = new(db);
        PasswordResetToken token = (await resets.FindByHashAsync(tokenHash, Ct))!;
        UserCredential credential = (await resets.FindCredentialOfUserAsync(token.UserId, Ct))!;
        token.Consume(Now);
        credential.ChangePassword(newHash);

        return await resets.ResetAsync(token, credential, Ct);
    }

    private async Task<T> RunAsync<T>(Func<PasswordResets, Task<T>> action)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await action(new PasswordResets(db));
    }

    private async Task RunAsync(Func<PasswordResets, Task> action)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await action(new PasswordResets(db));
    }
}