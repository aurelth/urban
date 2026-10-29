using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.IntegrationTests.Identity.Persistence;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumMappingTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task A_condominium_is_read_back_exactly_as_it_was_saved()
    {
        CondominiumEntity saved = await SeedCondominiumAsync();

        await using CondominiumDbContext db = postgres.CreateContext();
        CondominiumEntity read = await db.Condominiums.SingleAsync(c => c.Id == saved.Id, Ct);

        read.Should().BeEquivalentTo(saved);
        read.Address.Complement.Should().Be("Sala 2");
    }

    [Fact]
    public async Task A_person_is_read_back_exactly_as_it_was_saved()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        Person saved = await SeedManagerAsync(condominium);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Person read = await db.People.SingleAsync(p => p.Id == saved.Id, Ct);

        read.Should().BeEquivalentTo(saved);
        read.Cpf.Value.Should().Be(saved.Cpf.Value);
    }

    [Fact]
    public async Task An_address_without_complement_is_stored_as_null()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium(
            address: Urban.Condominium.Domain.Contacts.Address.Create("Rua A", "S/N", null, "Centro", "Blumenau", "SC", "89010-000").Value);
        await SaveAsync(condominium);

        await using CondominiumDbContext db = postgres.CreateContext();
        string? complement = await db.Database
            .SqlQuery<string?>($"""select address_complement as "Value" from condominium.condominium where id = {condominium.Id}""")
            .SingleAsync(Ct);

        complement.Should().BeNull();
    }

    [Fact]
    public async Task The_cpf_is_stored_encrypted_and_never_in_the_clear()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        Person person = await SeedManagerAsync(condominium);

        string stored = await ReadColumnAsync("cpf_encrypted", person.Id);

        stored.Should().StartWith("v1:");
        stored.Should().NotContain(person.Cpf.Value);
        postgres.Protector.Unprotect(stored).Should().Be(person.Cpf.Value);
    }

    [Fact]
    public async Task The_cpf_hash_is_stored_beside_it_and_matches_the_keyed_hash()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        Person person = await SeedManagerAsync(condominium);

        string hash = await ReadColumnAsync("cpf_hash", person.Id);

        hash.Should().Be(postgres.Protector.Hash(person.Cpf.Value));
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task The_cpf_hash_is_also_stamped_by_the_synchronous_save()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        Person person = CondominiumBuilders.Manager(condominium.Id);

        await using (CondominiumDbContext db = postgres.CreateContext(condominium.Id))
        {
            db.People.Add(person);
            db.SaveChanges();
        }

        (await ReadColumnAsync("cpf_hash", person.Id)).Should().Be(postgres.Protector.Hash(person.Cpf.Value));
    }

    [Fact]
    public async Task Two_saves_of_the_same_cpf_store_different_ciphertexts_but_the_same_hash()
    {
        Urban.Condominium.Domain.Documents.Cpf cpf = CondominiumBuilders.UniqueCpf();
        Person first = await SeedManagerAsync(await SeedCondominiumAsync(), cpf);
        Person second = await SeedManagerAsync(await SeedCondominiumAsync(), cpf);

        (await ReadColumnAsync("cpf_encrypted", first.Id)).Should().NotBe(await ReadColumnAsync("cpf_encrypted", second.Id));
        (await ReadColumnAsync("cpf_hash", first.Id)).Should().Be(await ReadColumnAsync("cpf_hash", second.Id));
    }

    [Fact]
    public async Task The_same_cpf_cannot_appear_twice_in_the_same_condominium()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        Urban.Condominium.Domain.Documents.Cpf cpf = CondominiumBuilders.UniqueCpf();
        await SeedManagerAsync(condominium, cpf);

        Func<Task> duplicate = () => SeedManagerAsync(condominium, cpf);

        await duplicate.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task The_same_cpf_can_belong_to_people_of_different_condominiums()
    {
        Urban.Condominium.Domain.Documents.Cpf cpf = CondominiumBuilders.UniqueCpf();

        Func<Task> both = async () =>
        {
            await SeedManagerAsync(await SeedCondominiumAsync(), cpf);
            await SeedManagerAsync(await SeedCondominiumAsync(), cpf);
        };

        await both.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Two_condominiums_cannot_share_a_cnpj()
    {
        CondominiumEntity first = await SeedCondominiumAsync();

        Func<Task> duplicate = () => SaveAsync(CondominiumBuilders.Condominium(cnpj: first.Cnpj));

        await duplicate.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task A_manager_cannot_register_two_condominiums()
    {
        Guid manager = Guid.CreateVersion7();
        await SaveAsync(CondominiumBuilders.Condominium(createdBy: manager));

        Func<Task> second = () => SaveAsync(CondominiumBuilders.Condominium(createdBy: manager));

        await second.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task A_user_has_at_most_one_person()
    {
        Guid user = Guid.CreateVersion7();
        await SeedManagerAsync(await SeedCondominiumAsync(), userId: user);

        Func<Task> second = async () => await SeedManagerAsync(await SeedCondominiumAsync(), userId: user);

        await second.ShouldBeRejectedByAsync(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task A_person_needs_an_existing_condominium()
    {
        Person orphan = CondominiumBuilders.Manager(Guid.CreateVersion7());

        Func<Task> save = async () =>
        {
            await using CondominiumDbContext db = postgres.CreateContext();
            db.People.Add(orphan);
            await db.SaveChangesAsync(Ct);
        };

        await save.ShouldBeRejectedByAsync(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task The_cnpj_is_stored_without_punctuation()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        await using CondominiumDbContext db = postgres.CreateContext();
        string stored = await db.Database
            .SqlQuery<string>($"""select cnpj as "Value" from condominium.condominium where id = {condominium.Id}""")
            .SingleAsync(Ct);

        stored.Should().Be(condominium.Cnpj.Value);
        stored.Should().MatchRegex("^[0-9A-Z]{14}$");
    }

    private async Task<CondominiumEntity> SeedCondominiumAsync()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        await SaveAsync(condominium);

        return condominium;
    }

    private async Task SaveAsync(CondominiumEntity condominium)
    {
        await using CondominiumDbContext db = postgres.CreateContext();
        db.Condominiums.Add(condominium);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Person> SeedManagerAsync(
        CondominiumEntity condominium,
        Urban.Condominium.Domain.Documents.Cpf? cpf = null,
        Guid? userId = null)
    {
        Person person = CondominiumBuilders.Manager(condominium.Id, userId, cpf);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        db.People.Add(person);
        await db.SaveChangesAsync(Ct);

        return person;
    }

    private async Task<string> ReadColumnAsync(string column, Guid personId)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        return column switch
        {
            "cpf_encrypted" => await db.Database
                .SqlQuery<string>($"""select cpf_encrypted as "Value" from condominium.person where id = {personId}""").SingleAsync(Ct),
            "cpf_hash" => await db.Database
                .SqlQuery<string>($"""select cpf_hash as "Value" from condominium.person where id = {personId}""").SingleAsync(Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
    }
}
