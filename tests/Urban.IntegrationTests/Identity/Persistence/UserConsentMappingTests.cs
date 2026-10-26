using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Identity.Domain.Consent;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class UserConsentMappingTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_consent_is_read_back_exactly_as_it_was_saved()
    {
        User user = await postgres.SeedUserAsync();
        UserConsent saved = UserConsent.Record(user.Id, "2026-10-26", Now);

        await SaveAsync(saved);

        await using IdentityDbContext db = postgres.CreateContext();
        (await db.UserConsents.AsNoTracking().SingleAsync(c => c.Id == saved.Id)).Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task A_consent_for_a_user_that_does_not_exist_is_rejected()
    {
        UserConsent orphan = UserConsent.Record(Guid.CreateVersion7(), "2026-10-26", Now);

        Func<Task> save = () => SaveAsync(orphan);

        await save.ShouldBeRejectedByAsync(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task A_user_keeps_every_version_they_accepted()
    {
        User user = await postgres.SeedUserAsync();
        await SaveAsync(UserConsent.Record(user.Id, "2026-10-26", Now));
        await SaveAsync(UserConsent.Record(user.Id, "2027-03-01", Now.AddDays(130)));

        await using IdentityDbContext db = postgres.CreateContext();
        string[] versions = await db.UserConsents.AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.AcceptedAt)
            .Select(c => c.Version)
            .ToArrayAsync(Ct);
        versions.Should().Equal("2026-10-26", "2027-03-01");
    }

    private async Task SaveAsync(UserConsent consent)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        db.UserConsents.Add(consent);
        await db.SaveChangesAsync(Ct);
    }
}