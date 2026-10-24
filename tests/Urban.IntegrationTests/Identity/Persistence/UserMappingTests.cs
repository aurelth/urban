using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class UserMappingTests(PostgresFixture postgres)
{
    public static TheoryData<AccountStatus> Statuses => new(Enum.GetValues<AccountStatus>());

    public static TheoryData<UserRole> Roles => new(Enum.GetValues<UserRole>());

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Theory]
    [MemberData(nameof(Statuses))]
    public async Task A_user_in_any_status_is_read_back_exactly_as_it_was_saved(AccountStatus status)
    {
        User saved = await postgres.SeedUserAsync(status);

        User? read = await FindByIdAsync(saved.Id);

        read.Should().BeEquivalentTo(saved);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task A_user_with_any_role_is_read_back_exactly_as_it_was_saved(UserRole role)
    {
        User saved = await postgres.SeedUserAsync(role: role);

        User? read = await FindByIdAsync(saved.Id);

        read.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task A_user_can_be_found_by_email_in_a_query_translated_to_sql()
    {
        User saved = await postgres.SeedUserAsync();

        await using IdentityDbContext db = postgres.CreateContext();
        User? found = await db.Users.SingleOrDefaultAsync(u => u.Email == saved.Email, Ct);

        found.Should().NotBeNull();
        found!.Id.Should().Be(saved.Id);
    }

    [Fact]
    public async Task Two_users_cannot_share_the_same_email()
    {
        User first = await postgres.SeedUserAsync();
        User duplicate = UserBuilder.InStatus(AccountStatus.Active, UserRole.Resident, first.Email.Value);

        await using IdentityDbContext db = postgres.CreateContext();
        db.Users.Add(duplicate);
        Func<Task> save = () => db.SaveChangesAsync(Ct);

        await save.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Status_and_role_are_stored_as_readable_text()
    {
        User saved = await postgres.SeedUserAsync(AccountStatus.Suspended, UserRole.Manager);

        await using IdentityDbContext db = postgres.CreateContext();
        string status = await db.Database.SqlQuery<string>($"""select status as "Value" from identity."user" where id = {saved.Id}""").SingleAsync(Ct);
        string role = await db.Database.SqlQuery<string>($"""select role as "Value" from identity."user" where id = {saved.Id}""").SingleAsync(Ct);

        status.Should().Be("Suspended");
        role.Should().Be("Manager");
    }

    private async Task<User?> FindByIdAsync(Guid id)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await db.Users.SingleOrDefaultAsync(u => u.Id == id, Ct);
    }
}
