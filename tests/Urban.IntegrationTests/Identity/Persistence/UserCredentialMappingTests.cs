using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class UserCredentialMappingTests(PostgresFixture postgres)
{
    private const string Hash = "$2a$04$hash-de-teste-que-nao-precisa-ser-verdadeiro";

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task A_credential_is_read_back_exactly_as_it_was_saved(int failures)
    {
        User user = await postgres.SeedUserAsync();
        UserCredential saved = UserCredential.Create(user.Id, Hash);
        for (int i = 0; i < failures; i++)
        {
            saved.RegisterFailure(UserBuilder.Now);
        }

        await SaveAsync(saved);

        (await postgres.ReadCredentialAsync(user.Id)).Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task A_credential_for_a_user_that_does_not_exist_is_rejected()
    {
        UserCredential orphan = UserCredential.Create(Guid.CreateVersion7(), Hash);

        Func<Task> save = () => SaveAsync(orphan);

        await save.ShouldBeRejectedByAsync(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task A_user_cannot_have_two_credentials()
    {
        User user = await postgres.SeedUserAsync();
        await SaveAsync(UserCredential.Create(user.Id, Hash));

        Func<Task> saveAnother = () => SaveAsync(UserCredential.Create(user.Id, Hash));

        await saveAnother.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Saving_a_copy_that_went_stale_is_rejected()
    {
        User user = await postgres.SeedUserAsync();
        await SaveAsync(UserCredential.Create(user.Id, Hash));

        await using IdentityDbContext first = postgres.CreateContext();
        await using IdentityDbContext second = postgres.CreateContext();
        UserCredential firstCopy = await first.UserCredentials.SingleAsync(c => c.UserId == user.Id, Ct);
        UserCredential secondCopy = await second.UserCredentials.SingleAsync(c => c.UserId == user.Id, Ct);
        firstCopy.RegisterFailure(UserBuilder.Now);
        await first.SaveChangesAsync(Ct);
        secondCopy.RegisterFailure(UserBuilder.Now);

        Func<Task> saveStale = () => second.SaveChangesAsync(Ct);

        await saveStale.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private async Task SaveAsync(UserCredential credential)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        db.UserCredentials.Add(credential);
        await db.SaveChangesAsync(Ct);
    }
}