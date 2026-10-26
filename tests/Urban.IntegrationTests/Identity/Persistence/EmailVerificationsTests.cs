using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Consent;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class EmailVerificationsTests(PostgresFixture postgres)
{
    private const string TermsVersion = "2026-10-26";

    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task TryRegister_stores_the_pending_user_and_no_link()
    {
        User user = NewUser();

        bool registered = await RunAsync(v => v.TryRegisterAsync(user, Ct));

        registered.Should().BeTrue();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Should().BeEquivalentTo(user);
        (await db.EmailVerificationTokens.AnyAsync(t => t.UserId == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task TryRegister_returns_false_and_stores_nothing_when_the_email_already_has_an_account()
    {
        User first = NewUser();
        await RunAsync(v => v.TryRegisterAsync(first, Ct));
        User second = User.Register(first.Email, UserRole.Manager, null, Now).Value;

        bool registered = await RunAsync(v => v.TryRegisterAsync(second, Ct));

        registered.Should().BeFalse();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.Users.AnyAsync(u => u.Id == second.Id)).Should().BeFalse();
        (await db.EmailVerificationTokens.AnyAsync(t => t.UserId == second.Id)).Should().BeFalse();
        (await db.Users.CountAsync(u => u.Email == first.Email)).Should().Be(1);
    }

    [Fact]
    public async Task Add_rejects_a_link_whose_hash_already_exists()
    {
        User user = await postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        string hash = NewHash();
        await RunAsync(v => v.AddAsync(EmailVerificationToken.Issue(user.Id, hash, Now), Ct));

        Func<Task> addDuplicate = () => RunAsync(v => v.AddAsync(EmailVerificationToken.Issue(user.Id, hash, Now), Ct));

        await addDuplicate.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Add_rejects_a_link_of_a_user_that_does_not_exist()
    {
        Func<Task> add = () => RunAsync(v => v.AddAsync(EmailVerificationToken.Issue(Guid.CreateVersion7(), NewHash(), Now), Ct));

        await add.ShouldBeRejectedByAsync(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task FindByHash_returns_the_stored_link_or_null()
    {
        (EmailVerificationToken token, _) = await SeedPendingAsync();

        EmailVerificationToken? found = await QueryAsync(v => v.FindByHashAsync(token.TokenHash, Ct));
        EmailVerificationToken? unknown = await QueryAsync(v => v.FindByHashAsync(NewHash(), Ct));

        found.Should().BeEquivalentTo(token);
        unknown.Should().BeNull();
    }

    [Fact]
    public async Task FindLatestOfUser_returns_the_most_recent_link()
    {
        User user = await postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        EmailVerificationToken older = EmailVerificationToken.Issue(user.Id, NewHash(), Now.AddMinutes(-10));
        EmailVerificationToken newer = EmailVerificationToken.Issue(user.Id, NewHash(), Now.AddMinutes(-2));
        await RunAsync(v => v.AddAsync(newer, Ct));
        await RunAsync(v => v.AddAsync(older, Ct));

        EmailVerificationToken? latest = await QueryAsync(v => v.FindLatestOfUserAsync(user.Id, Ct));

        latest!.Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task FindLatestOfUser_returns_null_when_the_user_has_no_link()
    {
        User user = await postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);

        EmailVerificationToken? latest = await QueryAsync(v => v.FindLatestOfUserAsync(user.Id, Ct));

        latest.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_saves_the_used_link_the_active_user_and_the_credential_together()
    {
        (EmailVerificationToken token, User user) = await SeedPendingAsync();
        UserCredential credential = UserCredential.Create(user.Id, "bcrypt-hash");

        bool confirmed = await ConfirmAsync(token.TokenHash, credential);

        confirmed.Should().BeTrue();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.EmailVerificationTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id)).UsedAt.Should().Be(Now);
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Status.Should().Be(AccountStatus.Active);
        (await postgres.ReadCredentialAsync(user.Id))!.PasswordHash.Should().Be("bcrypt-hash");
        UserConsent consent = await db.UserConsents.AsNoTracking().SingleAsync(c => c.UserId == user.Id);
        consent.Version.Should().Be(TermsVersion);
        consent.AcceptedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Confirm_saves_nothing_when_the_credential_cannot_be_stored()
    {
        (EmailVerificationToken token, User user) = await SeedPendingAsync();
        await using (IdentityDbContext seed = postgres.CreateContext())
        {
            seed.UserCredentials.Add(UserCredential.Create(user.Id, "existing-hash"));
            await seed.SaveChangesAsync();
        }

        bool confirmed = await ConfirmAsync(token.TokenHash, UserCredential.Create(user.Id, "new-hash"));

        confirmed.Should().BeFalse();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.EmailVerificationTokens.AsNoTracking().SingleAsync(t => t.Id == token.Id)).UsedAt.Should().BeNull();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id)).Status.Should().Be(AccountStatus.PendingVerification);
        (await postgres.ReadCredentialAsync(user.Id))!.PasswordHash.Should().Be("existing-hash");
        (await db.UserConsents.AnyAsync(c => c.UserId == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_lets_only_one_of_two_simultaneous_requests_win()
    {
        (EmailVerificationToken token, User user) = await SeedPendingAsync();
        await using IdentityDbContext firstDb = postgres.CreateContext();
        await using IdentityDbContext secondDb = postgres.CreateContext();
        EmailVerifications first = new(firstDb);
        EmailVerifications second = new(secondDb);
        EmailVerificationToken firstToken = (await first.FindByHashAsync(token.TokenHash, Ct))!;
        EmailVerificationToken secondToken = (await second.FindByHashAsync(token.TokenHash, Ct))!;
        User firstUser = await firstDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        User secondUser = await secondDb.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        firstToken.Consume(Now);
        secondToken.Consume(Now);
        firstUser.ConfirmEmail(Now);
        secondUser.ConfirmEmail(Now);

        bool firstWon = await first.ConfirmAsync(firstToken, firstUser, UserCredential.Create(user.Id, "first"), ConsentOf(user), Ct);
        bool secondWon = await second.ConfirmAsync(secondToken, secondUser, UserCredential.Create(user.Id, "second"), ConsentOf(user), Ct);

        firstWon.Should().BeTrue();
        secondWon.Should().BeFalse();
        (await postgres.ReadCredentialAsync(user.Id))!.PasswordHash.Should().Be("first");
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.UserConsents.CountAsync(c => c.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Confirm_returns_false_and_saves_nothing_when_the_link_was_used_in_between()
    {
        (EmailVerificationToken token, User owner) = await SeedPendingAsync();
        User other = await postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        await using IdentityDbContext firstDb = postgres.CreateContext();
        await using IdentityDbContext secondDb = postgres.CreateContext();
        EmailVerifications first = new(firstDb);
        EmailVerifications second = new(secondDb);
        EmailVerificationToken firstToken = (await first.FindByHashAsync(token.TokenHash, Ct))!;
        EmailVerificationToken secondToken = (await second.FindByHashAsync(token.TokenHash, Ct))!;
        User owned = await firstDb.Users.AsNoTracking().SingleAsync(u => u.Id == owner.Id, Ct);
        firstToken.Consume(Now);
        owned.ConfirmEmail(Now);
        await first.ConfirmAsync(firstToken, owned, UserCredential.Create(owner.Id, "first"), ConsentOf(owner), Ct);
        User unrelated = await secondDb.Users.AsNoTracking().SingleAsync(u => u.Id == other.Id, Ct);
        secondToken.Consume(Now);
        unrelated.ConfirmEmail(Now);

        // Nada mais conflitaria (outra conta, outra credencial): só o link já usado pode barrar esta gravação.
        bool confirmed = await second.ConfirmAsync(secondToken, unrelated, UserCredential.Create(other.Id, "second"), ConsentOf(other), Ct);

        confirmed.Should().BeFalse();
        await using IdentityDbContext db = postgres.CreateContext();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == other.Id)).Status.Should().Be(AccountStatus.PendingVerification);
        (await postgres.ReadCredentialAsync(other.Id)).Should().BeNull();
        (await db.UserConsents.AnyAsync(c => c.UserId == other.Id)).Should().BeFalse();
    }

    private static string NewHash() => Guid.NewGuid().ToString("N");

    private static UserConsent ConsentOf(User user) => UserConsent.Record(user.Id, TermsVersion, Now);

    private static User NewUser()
    {
        return User.Register(Email.Create($"{Guid.NewGuid():N}@example.com").Value, UserRole.Manager, null, Now).Value;
    }

    private async Task<(EmailVerificationToken Token, User User)> SeedPendingAsync()
    {
        User user = await postgres.SeedUserAsync(AccountStatus.PendingVerification, UserRole.Manager);
        EmailVerificationToken token = EmailVerificationToken.Issue(user.Id, NewHash(), Now.AddMinutes(-5));
        await RunAsync(v => v.AddAsync(token, Ct));
        return (token, user);
    }

    private async Task<bool> ConfirmAsync(string tokenHash, UserCredential credential)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        EmailVerifications verifications = new(db);
        EmailVerificationToken token = (await verifications.FindByHashAsync(tokenHash, Ct))!;
        User user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == token.UserId, Ct);
        token.Consume(Now);
        user.ConfirmEmail(Now);

        return await verifications.ConfirmAsync(token, user, credential, ConsentOf(user), Ct);
    }

    private async Task<T> RunAsync<T>(Func<EmailVerifications, Task<T>> action)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await action(new EmailVerifications(db));
    }

    private async Task RunAsync(Func<EmailVerifications, Task> action)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await action(new EmailVerifications(db));
    }

    private Task<T> QueryAsync<T>(Func<EmailVerifications, Task<T>> query) => RunAsync(query);
}