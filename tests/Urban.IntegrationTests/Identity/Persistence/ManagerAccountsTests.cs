using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Administration;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class ManagerAccountsTests(PostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset Base = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Listing_shows_only_manager_accounts_that_are_not_requests_and_were_not_deleted()
    {
        string tag = Tag();
        User active = await SeedAsync(Manager($"{tag}-ativo@example.com", AccountStatus.Active));
        User suspended = await SeedAsync(Manager($"{tag}-susp@example.com", AccountStatus.Suspended));
        User deactivated = await SeedAsync(Manager($"{tag}-desat@example.com", AccountStatus.Deactivated));
        User awaiting = await SeedAsync(Manager($"{tag}-aguard@example.com", AccountStatus.PendingVerification));
        await SeedAsync(
            Manager($"{tag}-pedido@example.com", AccountStatus.PendingApproval),
            Manager($"{tag}-recusado@example.com", AccountStatus.Rejected),
            Manager($"{tag}-apagado@example.com", AccountStatus.Deleted),
            UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, $"{tag}-morador@example.com"),
            UserBuilder.InStatus(AccountStatus.Active, UserRole.SuperAdmin, $"{tag}-admin@example.com"));

        ManagerPage page = await ListAsync(new ManagerFilter(null, tag), 1, 50);

        page.Total.Should().Be(4);
        page.Items.Select(u => u.Id).Should().BeEquivalentTo([active.Id, suspended.Id, deactivated.Id, awaiting.Id]);
    }

    [Fact]
    public async Task Listing_puts_the_newest_accounts_first()
    {
        string tag = Tag();
        User oldest = await SeedAsync(Manager($"{tag}-a@example.com", AccountStatus.Active, Base));
        User newest = await SeedAsync(Manager($"{tag}-c@example.com", AccountStatus.Active, Base.AddDays(2)));
        User middle = await SeedAsync(Manager($"{tag}-b@example.com", AccountStatus.Active, Base.AddDays(1)));

        ManagerPage page = await ListAsync(new ManagerFilter(null, tag), 1, 50);

        page.Items.Select(u => u.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public async Task Listing_by_status_keeps_only_that_status()
    {
        string tag = Tag();
        await SeedAsync(Manager($"{tag}-a@example.com", AccountStatus.Active));
        User suspended = await SeedAsync(Manager($"{tag}-b@example.com", AccountStatus.Suspended));

        ManagerPage page = await ListAsync(new ManagerFilter(AccountStatus.Suspended, tag), 1, 50);

        page.Total.Should().Be(1);
        page.Items.Single().Id.Should().Be(suspended.Id);
        page.Items.Single().StatusReason.Should().Be("Motivo de teste");
    }

    [Fact]
    public async Task Listing_pages_the_result_and_counts_the_whole_filtered_set()
    {
        string tag = Tag();
        for (int day = 0; day < 5; day++)
        {
            await SeedAsync(Manager($"{tag}-{day}@example.com", AccountStatus.Active, Base.AddDays(day)));
        }

        ManagerPage last = await ListAsync(new ManagerFilter(null, tag), 3, 2);
        ManagerPage first = await ListAsync(new ManagerFilter(null, tag), 1, 2);

        last.Total.Should().Be(5);
        last.Items.Should().ContainSingle().Which.Email.Value.Should().Be($"{tag}-0@example.com");
        first.Items.Select(u => u.Email.Value).Should().Equal($"{tag}-4@example.com", $"{tag}-3@example.com");
    }

    [Fact]
    public async Task Search_finds_the_email_the_name_and_the_condominium_typed_in_the_request_ignoring_case()
    {
        string byEmail = Tag();
        string byName = LettersTag();
        string byCondominium = Tag();
        User emailMatch = await SeedAsync(Manager($"{byEmail}@example.com", AccountStatus.Active));
        User nameMatch = await SeedAsync(Approved($"{Tag()}@example.com", $"Zélia {byName}", "Condomínio Qualquer"));
        User condominiumMatch = await SeedAsync(Approved($"{Tag()}@example.com", "Nome Qualquer", $"Edifício {byCondominium}"));

        (await ListAsync(new ManagerFilter(null, byEmail.ToUpperInvariant()), 1, 50)).Items.Select(u => u.Id).Should().Equal(emailMatch.Id);
        (await ListAsync(new ManagerFilter(null, $"zélia {byName}"), 1, 50)).Items.Select(u => u.Id).Should().Equal(nameMatch.Id);
        (await ListAsync(new ManagerFilter(null, $"EDIFÍCIO {byCondominium}"), 1, 50)).Items.Select(u => u.Id).Should().Equal(condominiumMatch.Id);
    }

    [Fact]
    public async Task Search_treats_percent_and_underscore_as_plain_characters()
    {
        string tag = Tag();
        await SeedAsync(Manager($"{tag}@example.com", AccountStatus.Active));

        (await ListAsync(new ManagerFilter(null, $"{tag}_"), 1, 50)).Total.Should().Be(0, "o _ não é curinga");
        (await ListAsync(new ManagerFilter(null, $"%{tag}"), 1, 50)).Total.Should().Be(0, "o % não é curinga");
        (await ListAsync(new ManagerFilter(null, tag), 1, 50)).Total.Should().Be(1);
    }

    [Fact]
    public async Task The_summary_counts_managers_by_status_requests_and_condominiums()
    {
        PlatformSummary before = await SummarizeAsync();
        string tag = Tag();
        Guid shared = Guid.CreateVersion7();
        await SeedAsync(
            Manager($"{tag}-a1@example.com", AccountStatus.Active, condominiumId: shared),
            Manager($"{tag}-a2@example.com", AccountStatus.Active, condominiumId: shared),
            Manager($"{tag}-a3@example.com", AccountStatus.Active),
            Manager($"{tag}-s1@example.com", AccountStatus.Suspended),
            Manager($"{tag}-d1@example.com", AccountStatus.Deactivated),
            Manager($"{tag}-v1@example.com", AccountStatus.PendingVerification),
            Manager($"{tag}-p1@example.com", AccountStatus.PendingApproval),
            Manager($"{tag}-p2@example.com", AccountStatus.PendingApproval),
            Manager($"{tag}-r1@example.com", AccountStatus.Rejected),
            Manager($"{tag}-x1@example.com", AccountStatus.Deleted),
            UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, $"{tag}-m1@example.com"));

        PlatformSummary after = await SummarizeAsync();

        (after.ActiveManagers - before.ActiveManagers).Should().Be(3);
        (after.SuspendedManagers - before.SuspendedManagers).Should().Be(1);
        (after.AwaitingConfirmationManagers - before.AwaitingConfirmationManagers).Should().Be(1);
        (after.PendingRequests - before.PendingRequests).Should().Be(2);
        (after.Condominiums - before.Condominiums).Should().Be(5, "o condomínio compartilhado conta uma vez; pedido, recusado, apagado e morador não contam");
    }

    [Fact]
    public async Task Changing_the_status_saves_the_new_status_who_did_it_when_and_why()
    {
        User manager = await SeedAsync(Manager($"{Tag()}@example.com", AccountStatus.Active));
        Guid admin = Guid.CreateVersion7();
        DateTimeOffset later = Base.AddHours(3);
        manager.Suspend(admin, "Uso indevido", later);

        bool changed = await TryChangeAsync(manager, AccountStatus.Active);

        changed.Should().BeTrue();
        User stored = await ReadAsync(manager.Id);
        stored.Status.Should().Be(AccountStatus.Suspended);
        stored.StatusChangedBy.Should().Be(admin);
        stored.StatusChangedAt.Should().Be(later);
        stored.StatusReason.Should().Be("Uso indevido");
    }

    [Fact]
    public async Task Changing_the_status_leaves_the_other_columns_alone()
    {
        User manager = await SeedAsync(Approved($"{Tag()}@example.com", "Ana Paula", "Vila Verde"));
        manager.ConfirmEmail(Base);
        manager.Suspend(Guid.CreateVersion7(), "Motivo", Base);

        await TryChangeAsync(manager, AccountStatus.PendingVerification);

        User stored = await ReadAsync(manager.Id);
        stored.DisplayName!.Value.Should().Be("Ana Paula");
        stored.RequestedCondominiumName!.Value.Should().Be("Vila Verde");
        stored.Email.Should().Be(manager.Email);
    }

    [Fact]
    public async Task Changing_the_status_of_an_account_that_is_no_longer_in_the_expected_status_changes_nothing_and_says_so()
    {
        User manager = await SeedAsync(Manager($"{Tag()}@example.com", AccountStatus.Active));
        User stale = await ReadAsync(manager.Id);
        manager.Suspend(Guid.CreateVersion7(), "Primeiro", Base);
        (await TryChangeAsync(manager, AccountStatus.Active)).Should().BeTrue();

        stale.Deactivate(Guid.CreateVersion7(), Base.AddHours(1));
        bool changed = await TryChangeAsync(stale, AccountStatus.Active);

        changed.Should().BeFalse();
        (await ReadAsync(manager.Id)).Status.Should().Be(AccountStatus.Suspended);
    }

    [Fact]
    public async Task Two_changes_at_the_same_time_let_exactly_one_win()
    {
        User manager = await SeedAsync(Manager($"{Tag()}@example.com", AccountStatus.Active));
        User first = await ReadAsync(manager.Id);
        User second = await ReadAsync(manager.Id);
        first.Suspend(Guid.CreateVersion7(), "Um", Base);
        second.Suspend(Guid.CreateVersion7(), "Dois", Base);

        bool[] outcomes = await Task.WhenAll(TryChangeAsync(first, AccountStatus.Active), TryChangeAsync(second, AccountStatus.Active));

        outcomes.Count(won => won).Should().Be(1);
    }

    [Fact]
    public async Task Changing_the_status_of_an_unknown_account_says_nothing_was_saved()
    {
        User ghost = Manager($"{Tag()}@example.com", AccountStatus.Active);
        ghost.Suspend(Guid.CreateVersion7(), "Motivo", Base);

        (await TryChangeAsync(ghost, AccountStatus.Active)).Should().BeFalse();
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>O nome de exibição só aceita letras: os dígitos do identificador viram g a p.</summary>
    private static string LettersTag() => string.Concat(Tag().Select(c => char.IsDigit(c) ? (char)('g' + (c - '0')) : c));

    private static User Manager(string address, AccountStatus status, DateTimeOffset? createdAt = null, Guid? condominiumId = null)
    {
        if (status is AccountStatus.PendingApproval or AccountStatus.Rejected)
        {
            return UserBuilder.Requested(status, address);
        }

        User user = User.Register(Email.Create(address).Value, UserRole.Manager, condominiumId ?? Guid.CreateVersion7(), createdAt ?? Base).Value;
        Guid admin = UserBuilder.ActorId;
        switch (status)
        {
            case AccountStatus.Active:
                user.ConfirmEmail(Base);
                break;
            case AccountStatus.Deactivated:
                user.ConfirmEmail(Base);
                user.Deactivate(admin, Base);
                break;
            case AccountStatus.Suspended:
                user.ConfirmEmail(Base);
                user.Suspend(admin, "Motivo de teste", Base);
                break;
            case AccountStatus.Deleted:
                user.Delete(admin, Base);
                break;
        }

        return user;
    }

    private static User Approved(string address, string name, string condominiumName)
    {
        User user = UserBuilder.Requested(AccountStatus.PendingApproval, address, name, condominiumName);
        user.Approve(UserBuilder.ActorId, Base);
        return user;
    }

    private async Task<User> SeedAsync(User user)
    {
        await SeedAsync([user]);
        return user;
    }

    private async Task SeedAsync(params User[] users)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        db.Users.AddRange(users);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<ManagerPage> ListAsync(ManagerFilter filter, int page, int pageSize)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new ManagerAccounts(db).ListAsync(filter, page, pageSize, Ct);
    }

    private async Task<PlatformSummary> SummarizeAsync()
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new ManagerAccounts(db).SummarizeAsync(Ct);
    }

    private async Task<bool> TryChangeAsync(User user, AccountStatus expected)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await new ManagerAccounts(db).TryChangeStatusAsync(user, expected, Ct);
    }

    private async Task<User> ReadAsync(Guid id)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
    }
}
