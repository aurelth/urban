using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>A leitura e a gravação do cadastro do síndico no PostgreSQL de verdade, com a versão (xmin) e o registro das mudanças.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumProfilesTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset At = new(2026, 10, 29, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Finding_the_registration_of_a_manager_returns_the_condominium_the_manager_and_a_version()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();

        ManagerCondominium? found = await FindAsync(condominium.Id, manager.UserId);

        found.Should().NotBeNull();
        found!.Condominium.Id.Should().Be(condominium.Id);
        found.Manager.Id.Should().Be(manager.Id);
        found.Condominium.Cnpj.Should().Be(condominium.Cnpj);
        found.Manager.Cpf.Should().Be(manager.Cpf);
        found.Version.Should().MatchRegex(@"^\d+\.\d+$");
    }

    [Fact]
    public async Task The_version_does_not_change_while_nothing_is_saved()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();

        string first = (await FindAsync(condominium.Id, manager.UserId))!.Version;
        string second = (await FindAsync(condominium.Id, manager.UserId))!.Version;

        second.Should().Be(first);
    }

    [Fact]
    public async Task A_request_of_another_condominium_finds_nothing_even_knowing_the_user_id()
    {
        (_, Person manager) = await SeedAsync();
        (CondominiumEntity other, _) = await SeedAsync();

        (await FindAsync(other.Id, manager.UserId)).Should().BeNull();
    }

    [Fact]
    public async Task A_request_without_a_tenant_finds_nothing()
    {
        (_, Person manager) = await SeedAsync();

        (await FindAsync(tenant: null, manager.UserId)).Should().BeNull();
    }

    [Fact]
    public async Task An_unknown_user_finds_nothing()
    {
        (CondominiumEntity condominium, _) = await SeedAsync();

        (await FindAsync(condominium.Id, Guid.CreateVersion7())).Should().BeNull();
    }

    [Fact]
    public async Task Saving_stores_the_changes_and_returns_a_new_version_that_matches_the_next_read()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumProfiles profiles = new(db);
        ManagerCondominium current = (await profiles.FindOfManagerAsync(manager.UserId, Ct))!;
        current.Condominium.Update("Residencial Bela Vista", current.Condominium.Address);
        current.Manager.UpdateContact(PhoneNumber.Create("(47) 98888-7777").Value, current.Manager.Address);

        Result<string> saved = await profiles.SaveAsync(current, Entry(condominium, manager, ChangeKind.Profile, "condominiumName", "phone"), Ct);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(current.Version);
        ManagerCondominium reread = (await FindAsync(condominium.Id, manager.UserId))!;
        reread.Version.Should().Be(saved.Value);
        reread.Condominium.Name.Should().Be("Residencial Bela Vista");
        reread.Manager.Phone.Value.Should().Be("47988887777");
    }

    [Fact]
    public async Task Two_edits_that_started_from_the_same_version_cannot_both_win()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();
        await using CondominiumDbContext first = postgres.CreateContext(condominium.Id);
        await using CondominiumDbContext second = postgres.CreateContext(condominium.Id);
        CondominiumProfiles firstProfiles = new(first);
        CondominiumProfiles secondProfiles = new(second);
        ManagerCondominium a = (await firstProfiles.FindOfManagerAsync(manager.UserId, Ct))!;
        ManagerCondominium b = (await secondProfiles.FindOfManagerAsync(manager.UserId, Ct))!;
        a.Manager.UpdateContact(PhoneNumber.Create("(47) 98888-7777").Value, a.Manager.Address);
        b.Manager.UpdateContact(PhoneNumber.Create("(47) 97777-6666").Value, b.Manager.Address);

        Result<string> winner = await firstProfiles.SaveAsync(a, Entry(condominium, manager, ChangeKind.Profile, "phone"), Ct);
        Result<string> loser = await secondProfiles.SaveAsync(b, Entry(condominium, manager, ChangeKind.Profile, "phone"), Ct);

        winner.IsSuccess.Should().BeTrue();
        loser.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        (await FindAsync(condominium.Id, manager.UserId))!.Manager.Phone.Value.Should().Be("47988887777");
    }

    [Fact]
    public async Task A_refused_save_leaves_no_change_log_and_a_clean_context()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();
        await using CondominiumDbContext first = postgres.CreateContext(condominium.Id);
        await using CondominiumDbContext second = postgres.CreateContext(condominium.Id);
        CondominiumProfiles firstProfiles = new(first);
        CondominiumProfiles secondProfiles = new(second);
        ManagerCondominium a = (await firstProfiles.FindOfManagerAsync(manager.UserId, Ct))!;
        ManagerCondominium b = (await secondProfiles.FindOfManagerAsync(manager.UserId, Ct))!;
        a.Manager.UpdateContact(PhoneNumber.Create("(47) 98888-7777").Value, a.Manager.Address);
        b.Manager.UpdateContact(PhoneNumber.Create("(47) 97777-6666").Value, b.Manager.Address);
        await firstProfiles.SaveAsync(a, Entry(condominium, manager, ChangeKind.Profile, "phone"), Ct);

        await secondProfiles.SaveAsync(b, Entry(condominium, manager, ChangeKind.Profile, "phone"), Ct);

        (await CountChangeLogAsync(condominium.Id)).Should().Be(1, "só a gravação que valeu deixa rastro");
        second.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task Changing_the_cpf_recalculates_the_hash_and_keeps_the_number_encrypted()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();
        string hashBefore = await ReadPersonColumnAsync(manager.Id, "cpf_hash");
        Cpf newCpf = CondominiumBuilders.UniqueCpf();
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumProfiles profiles = new(db);
        ManagerCondominium current = (await profiles.FindOfManagerAsync(manager.UserId, Ct))!;
        current.Manager.UpdateIdentity(current.Manager.FullName, newCpf, current.Manager.BirthDate);

        Result<string> saved = await profiles.SaveAsync(current, Entry(condominium, manager, ChangeKind.Identity, "cpf"), Ct);

        saved.IsSuccess.Should().BeTrue();
        (await ReadPersonColumnAsync(manager.Id, "cpf_hash")).Should().Be(postgres.Protector.Hash(newCpf.Value)).And.NotBe(hashBefore);
        string encrypted = await ReadPersonColumnAsync(manager.Id, "cpf_encrypted");
        encrypted.Should().StartWith("v1:").And.NotContain(newCpf.Value);
        (await FindAsync(condominium.Id, manager.UserId))!.Manager.Cpf.Should().Be(newCpf);
    }

    [Fact]
    public async Task A_cpf_already_used_by_another_person_of_the_condominium_is_a_conflict_and_changes_nothing()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();
        Cpf taken = CondominiumBuilders.UniqueCpf();
        await SeedAnotherPersonAsync(condominium, taken);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumProfiles profiles = new(db);
        ManagerCondominium current = (await profiles.FindOfManagerAsync(manager.UserId, Ct))!;
        current.Manager.UpdateIdentity(current.Manager.FullName, taken, current.Manager.BirthDate);

        Result<string> saved = await profiles.SaveAsync(current, Entry(condominium, manager, ChangeKind.Identity, "cpf"), Ct);

        saved.Error.Should().Be(PersonErrors.CpfAlreadyRegistered);
        (await FindAsync(condominium.Id, manager.UserId))!.Manager.Cpf.Should().Be(manager.Cpf);
        (await CountChangeLogAsync(condominium.Id)).Should().Be(0);
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task The_change_log_is_written_by_the_same_save_with_the_field_names_and_nothing_else()
    {
        (CondominiumEntity condominium, Person manager) = await SeedAsync();
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumProfiles profiles = new(db);
        ManagerCondominium current = (await profiles.FindOfManagerAsync(manager.UserId, Ct))!;
        current.Manager.UpdateContact(PhoneNumber.Create("(47) 98888-7777").Value, current.Manager.Address);

        await profiles.SaveAsync(current, Entry(condominium, manager, ChangeKind.Profile, "phone", "managerAddress"), Ct);

        await using CondominiumDbContext reader = postgres.CreateContext();
        (await reader.Database.SqlQuery<string>($"""select kind as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct))
            .Should().Be("profile");
        (await reader.Database.SqlQuery<string>($"""select array_to_string(fields, ',') as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct))
            .Should().Be("phone,managerAddress");
        (await reader.Database.SqlQuery<Guid>($"""select actor_user_id as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct))
            .Should().Be(manager.UserId);
        (await reader.Database.SqlQuery<DateTimeOffset>($"""select changed_at as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct))
            .Should().Be(At);
    }

    [Fact]
    public async Task The_change_log_table_has_no_column_that_could_hold_a_value()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        List<string> columns = await db.Database
            .SqlQuery<string>($"""select column_name as "Value" from information_schema.columns where table_schema = 'condominium' and table_name = 'change_log'""")
            .ToListAsync(Ct);

        columns.Should().BeEquivalentTo("id", "condominium_id", "actor_user_id", "kind", "fields", "changed_at");
    }

    private static ChangeLogEntry Entry(CondominiumEntity condominium, Person manager, ChangeKind kind, params string[] fields) =>
        new(condominium.Id, manager.UserId, kind, fields, At);

    private async Task<(CondominiumEntity Condominium, Person Manager)> SeedAsync()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person manager = CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy);
        await using CondominiumDbContext db = postgres.CreateContext();
        (await new CondominiumRegistrations(db).SaveAsync(condominium, manager, Ct)).IsSuccess.Should().BeTrue();

        return (condominium, manager);
    }

    private async Task SeedAnotherPersonAsync(CondominiumEntity condominium, Cpf cpf)
    {
        Person other = CondominiumBuilders.Manager(condominium.Id, Guid.CreateVersion7(), cpf);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        db.People.Add(other);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<ManagerCondominium?> FindAsync(Guid? tenant, Guid userId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(tenant);
        return await new CondominiumProfiles(db).FindOfManagerAsync(userId, Ct);
    }

    private async Task<int> CountChangeLogAsync(Guid condominiumId)
    {
        await using CondominiumDbContext db = postgres.CreateContext();
        return await db.Database.SqlQuery<int>($"""select count(*)::int as "Value" from condominium.change_log where condominium_id = {condominiumId}""").SingleAsync(Ct);
    }

    private async Task<string> ReadPersonColumnAsync(Guid personId, string column)
    {
        await using CondominiumDbContext db = postgres.CreateContext();
        return column switch
        {
            "cpf_encrypted" => await db.Database.SqlQuery<string>($"""select cpf_encrypted as "Value" from condominium.person where id = {personId}""").SingleAsync(Ct),
            "cpf_hash" => await db.Database.SqlQuery<string>($"""select cpf_hash as "Value" from condominium.person where id = {personId}""").SingleAsync(Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
    }
}
