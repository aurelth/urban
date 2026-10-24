using FluentAssertions;
using Npgsql;
using Urban.Identity.Domain.Tokens;
using Urban.Identity.Domain.Users;
using Urban.Identity.Infrastructure.Persistence;
using Urban.TestSupport.Identity;

namespace Urban.IntegrationTests.Identity.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class RefreshTokenRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = UserBuilder.Now;

    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Add_persists_the_token_so_it_can_be_found_by_its_hash()
    {
        User user = await postgres.SeedUserAsync();
        RefreshToken token = RefreshToken.Issue(user.Id, NewHash(), Now);

        await RunAsync(repository => repository.AddAsync(token, Ct));

        RefreshToken? found = await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct));
        found.Should().BeEquivalentTo(token);
    }

    [Fact]
    public async Task FindByHash_returns_null_when_the_hash_is_unknown()
    {
        RefreshToken? found = await QueryAsync(repository => repository.FindByHashAsync(NewHash(), Ct));

        found.Should().BeNull();
    }

    [Fact]
    public async Task Add_rejects_a_token_whose_hash_already_exists()
    {
        User user = await postgres.SeedUserAsync();
        string hash = NewHash();
        await RunAsync(repository => repository.AddAsync(RefreshToken.Issue(user.Id, hash, Now), Ct));

        Func<Task> addDuplicate = () => RunAsync(repository => repository.AddAsync(RefreshToken.Issue(user.Id, hash, Now), Ct));

        await addDuplicate.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Add_rejects_a_token_of_a_user_that_does_not_exist()
    {
        RefreshToken orphan = RefreshToken.Issue(Guid.CreateVersion7(), NewHash(), Now);

        Func<Task> add = () => RunAsync(repository => repository.AddAsync(orphan, Ct));

        await add.ShouldBeRejectedByAsync(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Update_persists_the_revocation_of_a_loaded_token()
    {
        User user = await postgres.SeedUserAsync();
        RefreshToken token = RefreshToken.Issue(user.Id, NewHash(), Now);
        await RunAsync(repository => repository.AddAsync(token, Ct));
        DateTimeOffset revokedAt = Now.AddMinutes(5);

        await RunAsync(async repository =>
        {
            RefreshToken loaded = (await repository.FindByHashAsync(token.TokenHash, Ct))!;
            loaded.Revoke(revokedAt);
            await repository.UpdateAsync(loaded, Ct);
        });

        RefreshToken? found = await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct));
        found!.RevokedAt.Should().Be(revokedAt);
    }

    [Fact]
    public async Task Update_also_persists_a_token_that_was_loaded_by_another_context()
    {
        User user = await postgres.SeedUserAsync();
        RefreshToken token = RefreshToken.Issue(user.Id, NewHash(), Now);
        await RunAsync(repository => repository.AddAsync(token, Ct));
        RefreshToken detached = (await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct)))!;
        detached.Revoke(Now.AddMinutes(5));

        await RunAsync(repository => repository.UpdateAsync(detached, Ct));

        RefreshToken? found = await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct));
        found!.RevokedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public async Task Replace_persists_the_revoked_token_and_its_replacement_together()
    {
        User user = await postgres.SeedUserAsync();
        RefreshToken token = RefreshToken.Issue(user.Id, NewHash(), Now);
        await RunAsync(repository => repository.AddAsync(token, Ct));
        string newHash = NewHash();
        DateTimeOffset rotatedAt = Now.AddMinutes(5);

        await RunAsync(async repository =>
        {
            RefreshToken current = (await repository.FindByHashAsync(token.TokenHash, Ct))!;
            RefreshToken replacement = current.Rotate(newHash, rotatedAt).Value;
            await repository.ReplaceAsync(current, replacement, Ct);
        });

        RefreshToken? revoked = await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct));
        RefreshToken? issued = await QueryAsync(repository => repository.FindByHashAsync(newHash, Ct));
        issued.Should().NotBeNull();
        issued!.RevokedAt.Should().BeNull();
        revoked!.RevokedAt.Should().Be(rotatedAt);
        revoked.ReplacedById.Should().Be(issued.Id);
    }

    [Fact]
    public async Task Replace_also_persists_a_current_token_that_was_loaded_by_another_context()
    {
        User user = await postgres.SeedUserAsync();
        RefreshToken token = RefreshToken.Issue(user.Id, NewHash(), Now);
        await RunAsync(repository => repository.AddAsync(token, Ct));
        RefreshToken detached = (await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct)))!;
        RefreshToken replacement = detached.Rotate(NewHash(), Now.AddMinutes(5)).Value;

        await RunAsync(repository => repository.ReplaceAsync(detached, replacement, Ct));

        RefreshToken? revoked = await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct));
        RefreshToken? issued = await QueryAsync(repository => repository.FindByHashAsync(replacement.TokenHash, Ct));
        revoked!.RevokedAt.Should().Be(Now.AddMinutes(5));
        revoked.ReplacedById.Should().Be(replacement.Id);
        issued.Should().NotBeNull();
    }

    [Fact]
    public async Task Replace_saves_nothing_when_the_replacement_cannot_be_saved()
    {
        User user = await postgres.SeedUserAsync();
        RefreshToken token = RefreshToken.Issue(user.Id, NewHash(), Now);
        RefreshToken other = RefreshToken.Issue(user.Id, NewHash(), Now);
        await RunAsync(async repository =>
        {
            await repository.AddAsync(token, Ct);
            await repository.AddAsync(other, Ct);
        });

        Func<Task> replaceWithTakenHash = () => RunAsync(async repository =>
        {
            RefreshToken current = (await repository.FindByHashAsync(token.TokenHash, Ct))!;
            RefreshToken replacement = current.Rotate(other.TokenHash, Now.AddMinutes(5)).Value;
            await repository.ReplaceAsync(current, replacement, Ct);
        });

        await replaceWithTakenHash.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
        RefreshToken? found = await QueryAsync(repository => repository.FindByHashAsync(token.TokenHash, Ct));
        found!.RevokedAt.Should().BeNull("a revogação e o substituto são gravados juntos ou nenhum dos dois");
    }

    [Fact]
    public async Task RevokeAllOfUser_revokes_only_the_active_tokens_of_that_user()
    {
        User user = await postgres.SeedUserAsync();
        User bystander = await postgres.SeedUserAsync();
        RefreshToken active = RefreshToken.Issue(user.Id, NewHash(), Now);
        RefreshToken alreadyRevoked = RefreshToken.Issue(user.Id, NewHash(), Now);
        DateTimeOffset earlier = Now.AddMinutes(1);
        alreadyRevoked.Revoke(earlier);
        RefreshToken ofBystander = RefreshToken.Issue(bystander.Id, NewHash(), Now);
        await RunAsync(async repository =>
        {
            await repository.AddAsync(active, Ct);
            await repository.AddAsync(alreadyRevoked, Ct);
            await repository.AddAsync(ofBystander, Ct);
        });
        DateTimeOffset revokedAt = Now.AddMinutes(10);

        await RunAsync(repository => repository.RevokeAllOfUserAsync(user.Id, revokedAt, Ct));

        (await QueryAsync(repository => repository.FindByHashAsync(active.TokenHash, Ct)))!.RevokedAt.Should().Be(revokedAt);
        (await QueryAsync(repository => repository.FindByHashAsync(alreadyRevoked.TokenHash, Ct)))!.RevokedAt.Should().Be(earlier);
        (await QueryAsync(repository => repository.FindByHashAsync(ofBystander.TokenHash, Ct)))!.RevokedAt.Should().BeNull();
    }

    private static string NewHash() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    /// <summary>Cada operação usa um contexto novo, como acontece em cada requisição.</summary>
    private async Task RunAsync(Func<RefreshTokenRepository, Task> action)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        await action(new RefreshTokenRepository(db));
    }

    private async Task<T> QueryAsync<T>(Func<RefreshTokenRepository, Task<T>> query)
    {
        await using IdentityDbContext db = postgres.CreateContext();
        return await query(new RefreshTokenRepository(db));
    }
}
