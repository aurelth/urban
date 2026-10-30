using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class EmailChangesTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Add_rejects_a_link_whose_hash_already_exists()
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        string hash = NewHash();
        await RunAsync(r => r.AddAsync(Issue(user.Id, hash, Now), Ct));

        Func<Task> addDuplicate = () => RunAsync(r => r.AddAsync(Issue(user.Id, hash, Now), Ct));

        await addDuplicate.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Add_rejects_a_link_of_a_user_that_does_not_exist()
    {
        Func<Task> add = () => RunAsync(r => r.AddAsync(Issue(Guid.CreateVersion7(), NewHash(), Now), Ct));

        await add.ShouldBeRejectedByAsync(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task The_new_address_is_stored_with_the_link_and_the_secret_only_as_a_hash()
    {
        (EmailChangeToken token, _) = await SeedLinkAsync("nova@example.com");

        EmailChangeToken? found = await RunAsync(r => r.FindByHashAsync(token.TokenHash, Ct));

        found!.NewEmail.Value.Should().Be("nova@example.com");
        found.TokenHash.Should().Be(token.TokenHash);
        found.ExpiresAt.Should().Be(Now.AddMinutes(-5).AddHours(24));
        (await RunAsync(r => r.FindByHashAsync(NewHash(), Ct))).Should().BeNull();
    }

    [Fact]
    public async Task FindLatestOfUser_returns_the_most_recent_link_and_ignores_other_users()
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        User other = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        EmailChangeToken older = Issue(user.Id, NewHash(), Now.AddMinutes(-10));
        EmailChangeToken newer = Issue(user.Id, NewHash(), Now.AddMinutes(-2));
        await RunAsync(r => r.AddAsync(older, Ct));
        await RunAsync(r => r.AddAsync(newer, Ct));

        (await RunAsync(r => r.FindLatestOfUserAsync(user.Id, Ct)))!.Id.Should().Be(newer.Id);
        (await RunAsync(r => r.FindLatestOfUserAsync(other.Id, Ct))).Should().BeNull();
    }

    [Fact]
    public async Task Add_uses_up_the_links_still_open_of_the_same_user_and_leaves_the_others_alone()
    {
        (EmailChangeToken previous, User user) = await SeedLinkAsync("primeira@example.com");
        EmailChangeToken othersLink = (await SeedLinkAsync("outra@example.com")).Token;
        EmailChangeToken replacement = Issue(user.Id, NewHash(), Now);

        await RunAsync(r => r.AddAsync(replacement, Ct));

        await using IdentityDbContext db = postgres.CreateContext();
        (await db.EmailChangeTokens.AsNoTracking().SingleAsync(t => t.Id == previous.Id, Ct)).UsedAt.Should().Be(Now);
        (await db.EmailChangeTokens.AsNoTracking().SingleAsync(t => t.Id == replacement.Id, Ct)).UsedAt.Should().BeNull();
        (await db.EmailChangeTokens.AsNoTracking().SingleAsync(t => t.Id == othersLink.Id, Ct)).UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_swaps_the_address_and_marks_the_link_as_used_together()
    {
        (EmailChangeToken token, User user) = await SeedLinkAsync(UniqueAddress());

        bool confirmed = await ConfirmAsync(token.TokenHash);

        confirmed.Should().BeTrue();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.EmailChangeTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id, Ct)).UsedAt.Should().Be(Now);
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct)).Email.Value.Should().Be(token.NewEmail.Value);
    }

    [Fact]
    public async Task Confirm_also_uses_up_the_other_links_still_open_of_the_same_user()
    {
        (EmailChangeToken token, User user) = await SeedLinkAsync(UniqueAddress());
        EmailChangeToken sibling = Issue(user.Id, NewHash(), Now.AddMinutes(-3));
        await using (IdentityDbContext seed = postgres.CreateContext())
        {
            seed.EmailChangeTokens.Add(sibling);
            await seed.SaveChangesAsync(Ct);
        }

        await ConfirmAsync(token.TokenHash);

        await using IdentityDbContext db = postgres.CreateContext();
        (await db.EmailChangeTokens.AsNoTracking().SingleAsync(t => t.Id == sibling.Id, Ct)).UsedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Confirm_lets_only_one_of_two_simultaneous_requests_win()
    {
        (EmailChangeToken token, User user) = await SeedLinkAsync(UniqueAddress());
        await using IdentityDbContext firstDb = postgres.CreateContext();
        await using IdentityDbContext secondDb = postgres.CreateContext();
        EmailChanges first = new(firstDb);
        EmailChanges second = new(secondDb);
        (EmailChangeToken firstToken, User firstUser) = await LoadAsync(firstDb, first, token.TokenHash, user.Id);
        (EmailChangeToken secondToken, User secondUser) = await LoadAsync(secondDb, second, token.TokenHash, user.Id);
        firstToken.Consume(Now);
        secondToken.Consume(Now);
        firstUser.ChangeEmail(firstToken.NewEmail);
        secondUser.ChangeEmail(secondToken.NewEmail);

        bool firstWon = await first.ConfirmAsync(firstToken, firstUser, Ct);
        bool secondWon = await second.ConfirmAsync(secondToken, secondUser, Ct);

        firstWon.Should().BeTrue();
        secondWon.Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_returns_false_and_keeps_everything_when_the_address_was_taken_in_between()
    {
        (EmailChangeToken token, User user) = await SeedLinkAsync(UniqueAddress());
        string taken = token.NewEmail.Value;
        await using (IdentityDbContext seed = postgres.CreateContext())
        {
            seed.Users.Add(UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, taken));
            await seed.SaveChangesAsync(Ct);
        }

        bool confirmed = await ConfirmAsync(token.TokenHash);

        confirmed.Should().BeFalse();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.EmailChangeTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id, Ct)).UsedAt.Should().BeNull();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct)).Email.Value.Should().NotBe(taken);
    }

    [Fact]
    public async Task Confirm_refuses_a_link_that_was_not_marked_as_used()
    {
        (EmailChangeToken token, User user) = await SeedLinkAsync("nova@example.com");
        await using IdentityDbContext db = postgres.CreateContext();
        EmailChanges changes = new(db);
        (EmailChangeToken loaded, User loadedUser) = await LoadAsync(db, changes, token.TokenHash, user.Id);

        Func<Task> confirm = () => changes.ConfirmAsync(loaded, loadedUser, Ct);

        await confirm.Should().ThrowAsync<InvalidOperationException>();
    }

    private static string UniqueAddress() => $"{Guid.NewGuid():N}@example.com";

    private static string NewHash() => Guid.NewGuid().ToString("N");

    private static EmailChangeToken Issue(Guid userId, string hash, DateTimeOffset at, string address = "destino@example.com") =>
        EmailChangeToken.Issue(userId, Email.Create(address).Value, hash, at);

    private async Task<(EmailChangeToken Token, User User)> SeedLinkAsync(string address)
    {
        User user = await postgres.SeedUserWithPasswordAsync("Senha-segura-123");
        EmailChangeToken token = Issue(user.Id, NewHash(), Now.AddMinutes(-5), address);
        await RunAsync(r => r.AddAsync(token, Ct));

        return (token, user);
    }

    private static async Task<(EmailChangeToken Token, User User)> LoadAsync(IdentityDbContext db, EmailChanges changes, string tokenHash, Guid userId) =>
        ((await changes.FindByHashAsync(tokenHash, Ct))!, await db.Users.SingleAsync(u => u.Id == userId, Ct));

    private async Task<bool> ConfirmAsync(string tokenHash)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        EmailChanges changes = new(db);
        EmailChangeToken token = (await changes.FindByHashAsync(tokenHash, Ct))!;
        User user = await db.Users.SingleAsync(u => u.Id == token.UserId, Ct);
        token.Consume(Now);
        user.ChangeEmail(token.NewEmail);

        return await changes.ConfirmAsync(token, user, Ct);
    }

    private async Task<T> RunAsync<T>(Func<EmailChanges, Task<T>> action)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await action(new EmailChanges(db));
    }

    private async Task RunAsync(Func<EmailChanges, Task> action)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await action(new EmailChanges(db));
    }
}
