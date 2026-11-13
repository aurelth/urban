using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.RegistrationRequests;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class RegistrationRequestsTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Listing_returns_the_pending_requests_oldest_first_with_the_data_the_screen_needs()
    {
        User older = NewRequest("Ana Paula", "Residencial Vila Verde", UserBuilder.Now.AddMinutes(-10));
        User newer = NewRequest("Bia Souza", "Edifício Sol", UserBuilder.Now);
        await SaveAsync(newer, older);

        IReadOnlyList<RegistrationRequestSummary> pending = await ListAsync();

        RegistrationRequestSummary[] mine = pending.Where(r => r.Id == older.Id || r.Id == newer.Id).ToArray();
        mine.Select(r => r.Id).Should().Equal(older.Id, newer.Id);
        mine[0].Should().Be(new RegistrationRequestSummary(older.Id, older.Email.Value, "Ana Paula", "Residencial Vila Verde", older.CreatedAt));
    }

    [Fact]
    public async Task Listing_leaves_out_requests_that_were_already_decided_and_accounts_that_are_not_requests()
    {
        User approved = NewRequest("Ana Paula", "Vila Verde", UserBuilder.Now);
        approved.Approve(UserBuilder.ActorId, UserBuilder.Now);
        User rejected = NewRequest("Bia Souza", "Edifício Sol", UserBuilder.Now);
        rejected.Reject(UserBuilder.ActorId, UserBuilder.Now);
        User active = await postgres.SeedUserAsync(AccountStatus.Active, UserRole.Manager);
        await SaveAsync(approved, rejected);

        IReadOnlyList<RegistrationRequestSummary> pending = await ListAsync();

        pending.Select(r => r.Id).Should().NotContain([approved.Id, rejected.Id, active.Id]);
    }

    [Fact]
    public async Task Deciding_saves_the_new_status_and_who_decided_when()
    {
        User request = NewRequest("Ana Paula", "Vila Verde", UserBuilder.Now);
        await SaveAsync(request);
        DateTimeOffset later = UserBuilder.Now.AddHours(1);
        request.Reject(UserBuilder.ActorId, later);

        bool decided = await TryDecideAsync(request, AccountStatus.PendingApproval);

        decided.Should().BeTrue();
        User stored = await ReadAsync(request.Id);
        stored.Status.Should().Be(AccountStatus.Rejected);
        stored.StatusChangedBy.Should().Be(UserBuilder.ActorId);
        stored.StatusChangedAt.Should().Be(later);
        stored.DisplayName!.Value.Should().Be("Ana Paula");
        stored.RequestedCondominiumName!.Value.Should().Be("Vila Verde");
    }

    [Fact]
    public async Task Deciding_a_request_that_is_no_longer_pending_changes_nothing_and_says_so()
    {
        User request = NewRequest("Ana Paula", "Vila Verde", UserBuilder.Now);
        await SaveAsync(request);
        User stale = await ReadAsync(request.Id);
        request.Approve(UserBuilder.ActorId, UserBuilder.Now);
        (await TryDecideAsync(request, AccountStatus.PendingApproval)).Should().BeTrue();

        stale.Reject(Guid.CreateVersion7(), UserBuilder.Now.AddHours(1));
        bool decided = await TryDecideAsync(stale, AccountStatus.PendingApproval);

        decided.Should().BeFalse();
        (await ReadAsync(request.Id)).Status.Should().Be(AccountStatus.PendingVerification);
    }

    [Fact]
    public async Task Two_decisions_at_the_same_time_let_exactly_one_win()
    {
        User request = NewRequest("Ana Paula", "Vila Verde", UserBuilder.Now);
        await SaveAsync(request);
        User toApprove = await ReadAsync(request.Id);
        User toReject = await ReadAsync(request.Id);
        toApprove.Approve(UserBuilder.ActorId, UserBuilder.Now);
        toReject.Reject(UserBuilder.ActorId, UserBuilder.Now);

        bool[] outcomes = await Task.WhenAll(
            TryDecideAsync(toApprove, AccountStatus.PendingApproval),
            TryDecideAsync(toReject, AccountStatus.PendingApproval));

        outcomes.Count(won => won).Should().Be(1);
    }

    [Fact]
    public async Task Deciding_an_unknown_user_says_nothing_was_saved()
    {
        User ghost = NewRequest("Ana Paula", "Vila Verde", UserBuilder.Now);
        ghost.Approve(UserBuilder.ActorId, UserBuilder.Now);

        (await TryDecideAsync(ghost, AccountStatus.PendingApproval)).Should().BeFalse();
    }

    private static User NewRequest(string name, string condominium, DateTimeOffset now) =>
        User.RequestManagerAccess(
            Email.Create($"{Guid.NewGuid():N}@example.com").Value,
            DisplayName.Create(name).Value,
            RequestedCondominiumName.Create(condominium).Value,
            now);

    private async Task SaveAsync(params User[] users)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        db.Users.AddRange(users);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<IReadOnlyList<RegistrationRequestSummary>> ListAsync()
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new RegistrationRequests(db).ListPendingAsync(Ct);
    }

    private async Task<bool> TryDecideAsync(User user, AccountStatus expected)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new RegistrationRequests(db).TryDecideAsync(user, expected, Ct);
    }

    private async Task<User> ReadAsync(Guid id)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
    }
}
