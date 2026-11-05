using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>As unidades no PostgreSQL de verdade: índices, concorrência, versão, ordem natural, lote e registro de alterações.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumUnitsTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset At = new(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);

    // ---- Esquema e migration ----

    [Fact]
    public async Task The_migration_creates_the_unit_table_with_a_unique_index_on_the_identifier()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        List<string> tables = await db.Database
            .SqlQuery<string>($"""select table_name as "Value" from information_schema.tables where table_schema = 'condominium'""")
            .ToListAsync(Ct);
        string index = await db.Database
            .SqlQuery<string>($"""select indexdef as "Value" from pg_indexes where schemaname = 'condominium' and indexname = 'ix_unit_identifier'""")
            .SingleAsync(Ct);

        tables.Should().Contain("unit");
        index.Should().Contain("UNIQUE").And.Contain("condominium_id").And.Contain("lower");
    }

    [Theory]
    [InlineData("block", "character varying", 30)]
    [InlineData("number", "character varying", 20)]
    [InlineData("type", "character varying", 20)]
    public async Task The_columns_have_the_length_of_the_domain_rules(string column, string type, int length)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        ColumnInfo info = await db.Database.SqlQuery<ColumnInfo>($"""
            select data_type as data_type, character_maximum_length as length from information_schema.columns
            where table_schema = 'condominium' and table_name = 'unit' and column_name = {column}
            """).SingleAsync(Ct);

        info.DataType.Should().Be(type);
        info.Length.Should().Be(length);
    }

    [Fact]
    public async Task The_fraction_and_the_area_have_the_exact_precision_of_the_rules()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        PrecisionInfo fraction = await Precision(db, "ideal_fraction");
        PrecisionInfo area = await Precision(db, "area");

        (fraction.Precision, fraction.Scale).Should().Be((9, 8));
        (area.Precision, area.Scale).Should().Be((7, 2));
    }

    [Fact]
    public async Task Condominiums_that_already_existed_before_the_migration_stay_with_the_ideal_fraction_apportionment()
    {
        string database = $"urban_migration_{Guid.NewGuid():N}";
        await using (NpgsqlConnection admin = new(postgres.ConnectionString))
        {
            await admin.OpenAsync(Ct);
            await using NpgsqlCommand create = new($"create database {database}", admin);
            await create.ExecuteNonQueryAsync(Ct);
        }

        string connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = database, Pooling = false }.ConnectionString;
        await using CondominiumDbContext db = new(CondominiumPersistence.CreateOptions(connectionString), postgres.Protector, new FixedTenant(null));
        IMigrator migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync("20261030024814_AddProfileEditing", Ct);
        Guid id = Guid.CreateVersion7();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into condominium.condominium (id, name, cnpj, address_street, address_number, address_neighborhood, address_city, address_state, address_postal_code, created_by, created_at)
            values ({id}, 'Antigo', '11222333000181', 'Rua A', '1', 'Centro', 'Blumenau', 'SC', '89010000', {Guid.CreateVersion7()}, now())
            """, Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);

        string method = await db.Database
            .SqlQuery<string>($"""select apportionment_method as "Value" from condominium.condominium where id = {id}""")
            .SingleAsync(Ct);
        method.Should().Be("IdealFraction");
    }

    // ---- Identificador único ----

    [Fact]
    public async Task The_identifier_is_unique_per_condominium_ignoring_case_and_treating_no_block_as_empty()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, "Bloco A", "101"));

        Result<string> sameIgnoringCase = await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, "BLOCO a", "101"));
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "101"));
        Result<string> noBlockAgain = await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "101"));
        Result<string> otherBlock = await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, "Bloco B", "101"));

        sameIgnoringCase.Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
        noBlockAgain.Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
        otherBlock.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task The_same_identifier_can_exist_in_two_different_condominiums()
    {
        CondominiumEntity first = await SeedCondominiumAsync();
        CondominiumEntity second = await SeedCondominiumAsync();

        (await AddAsync(first, UnitBuilders.Unit(first.Id, null, "101"))).IsSuccess.Should().BeTrue();
        (await AddAsync(second, UnitBuilders.Unit(second.Id, null, "101"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_deactivated_unit_keeps_reserving_its_identifier()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, null, "101");
        await AddAsync(condominium, unit);
        UnitRecord record = (await FindAsync(condominium, unit.Id))!;
        record.Unit.Deactivate(At);
        await SaveAsync(condominium, record);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        (await new CondominiumUnits(db).IsIdentityTakenAsync(unit.Identity, null, Ct)).Should().BeTrue();
        (await new CondominiumUnits(db).IsIdentityTakenAsync(unit.Identity, unit.Id, Ct)).Should().BeFalse();
        (await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "101"))).Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
    }

    [Fact]
    public async Task Two_simultaneous_creations_of_the_same_identifier_let_one_through_and_give_the_other_a_conflict_never_a_500()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        Result<string>[] results = await Task.WhenAll(
            AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "777")),
            AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "777")));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
    }

    [Fact]
    public async Task Two_simultaneous_creations_that_together_pass_one_hundred_percent_let_only_one_through()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        Result<string>[] results = await Task.WhenAll(
            AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "1", idealFraction: 0.6m)),
            AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "2", idealFraction: 0.6m)));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Should().Be(UnitErrors.FractionSumExceeded);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        (await new CondominiumUnits(db).ActiveFractionTotalAsync(null, Ct)).Should().Be(0.6m);
    }

    [Fact]
    public async Task The_unique_index_is_the_second_barrier_a_row_written_outside_the_lock_becomes_a_conflict_never_an_exception()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit outsider = UnitBuilders.Unit(condominium.Id, null, "808");
        CondominiumUnit mine = UnitBuilders.Unit(condominium.Id, null, "808");

        // Uma gravação que não passou pela trava da linha do condomínio e ainda não foi confirmada: a conferência não a vê,
        // então só o índice único do banco pode barrar a unidade que chega depois, quando a primeira confirmar.
        await using CondominiumDbContext other = postgres.CreateContext(condominium.Id);
        await using IDbContextTransaction open = await other.Database.BeginTransactionAsync(Ct);
        other.Units.Add(outsider);
        await other.SaveChangesAsync(Ct);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Task<Result<string>> pending = new CondominiumUnits(db).AddAsync(mine, Entry(condominium, mine.Id, "number"), Ct);
        await Task.Delay(500, Ct);
        pending.IsCompleted.Should().BeFalse("o banco segura a inserção até saber se a primeira foi confirmada");
        await open.CommitAsync(Ct);

        (await pending).Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
        (await ChangeLogCountAsync(condominium.Id)).Should().Be(0);
    }

    // ---- Tenant, versão e valores ----

    [Fact]
    public async Task A_condominium_sees_only_its_own_units_and_without_a_tenant_nothing_is_visible()
    {
        CondominiumEntity a = await SeedCondominiumAsync();
        CondominiumEntity b = await SeedCondominiumAsync();
        CondominiumUnit unitA = UnitBuilders.Unit(a.Id, null, "A1");
        CondominiumUnit unitB = UnitBuilders.Unit(b.Id, null, "B1");
        await AddAsync(a, unitA);
        await AddAsync(b, unitB);

        await using CondominiumDbContext asA = postgres.CreateContext(a.Id);
        await using CondominiumDbContext withoutTenant = postgres.CreateContext(null);

        (await asA.Units.Select(u => u.Id).ToListAsync(Ct)).Should().Contain(unitA.Id).And.NotContain(unitB.Id);
        (await new CondominiumUnits(asA).FindAsync(unitB.Id, Ct)).Should().BeNull();
        (await withoutTenant.Units.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task The_version_changes_with_every_change_and_saving_with_an_old_version_is_a_conflict()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, null, "101");
        await AddAsync(condominium, unit);

        await using CondominiumDbContext first = postgres.CreateContext(condominium.Id);
        await using CondominiumDbContext second = postgres.CreateContext(condominium.Id);
        UnitRecord reading1 = (await new CondominiumUnits(first).FindAsync(unit.Id, Ct))!;
        UnitRecord reading2 = (await new CondominiumUnits(second).FindAsync(unit.Id, Ct))!;
        reading1.Version.Should().Be(reading2.Version);

        reading1.Unit.Update(null, "101", UnitType.Apartment, null, 70m);
        Result<string> saved = await new CondominiumUnits(first).SaveAsync(reading1, Entry(condominium, unit.Id, "area"), Ct);
        reading2.Unit.Update(null, "101", UnitType.Apartment, null, 80m);
        Result<string> stale = await new CondominiumUnits(second).SaveAsync(reading2, Entry(condominium, unit.Id, "area"), Ct);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(reading1.Version);
        stale.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        UnitRecord current = (await FindAsync(condominium, unit.Id))!;
        current.Version.Should().Be(saved.Value);
        current.Unit.Area.Should().Be(70m);
    }

    [Fact]
    public async Task The_fraction_and_the_area_come_back_from_the_database_exactly_as_they_went_in()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, "Bloco A", "101", UnitType.House, 0.12345678m, 99999.99m);
        await AddAsync(condominium, unit);

        CondominiumUnit read = (await FindAsync(condominium, unit.Id))!.Unit;

        read.IdealFraction.Should().Be(0.12345678m);
        read.Area.Should().Be(99999.99m);
        read.Type.Should().Be(UnitType.House);
        read.Block.Should().Be("Bloco A");
        read.CreatedAt.Should().Be(UnitBuilders.Now);
        read.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivating_keeps_the_row_and_the_date_and_nothing_is_ever_deleted()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, null, "101");
        await AddAsync(condominium, unit);
        UnitRecord record = (await FindAsync(condominium, unit.Id))!;
        record.Unit.Deactivate(At);
        await SaveAsync(condominium, record);

        UnitRecord read = (await FindAsync(condominium, unit.Id))!;

        read.Unit.IsActive.Should().BeFalse();
        read.Unit.DeactivatedAt.Should().Be(At);
        await using CondominiumDbContext db = postgres.CreateContext();
        (await db.Database.SqlQuery<int>($"""select count(*)::int as "Value" from condominium.unit where id = {unit.Id}""").SingleAsync(Ct)).Should().Be(1);
    }

    // ---- Consultas ----

    [Fact]
    public async Task The_natural_order_comes_from_the_database_with_a_correct_page_and_total()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        foreach (int number in Enumerable.Range(1, 25).Reverse())
        {
            await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, number.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumUnits units = new(db);
        PagedUnits all = await units.ListAsync(new UnitFilter(null, null, UnitStatusFilter.All, null), 1, 200, Ct);
        PagedUnits second = await units.ListAsync(new UnitFilter(null, null, UnitStatusFilter.All, null), 2, 10, Ct);
        PagedUnits beyond = await units.ListAsync(new UnitFilter(null, null, UnitStatusFilter.All, null), 9, 10, Ct);

        all.Items.Select(i => i.Unit.Number).Should().Equal(Enumerable.Range(1, 25).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        second.Items.Select(i => i.Unit.Number).Should().Equal(Enumerable.Range(11, 10).Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        second.Total.Should().Be(25);
        beyond.Items.Should().BeEmpty();
        beyond.Total.Should().Be(25);
    }

    [Fact]
    public async Task The_order_puts_the_block_first_the_empty_block_before_the_others_and_house_two_before_house_ten()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        foreach ((string? block, string number) in new (string?, string)[] { ("Bloco 10", "1"), ("Bloco 2", "5"), (null, "Casa 10"), (null, "Casa 2"), ("Bloco 2", "1") })
        {
            await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, block, number));
        }

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        PagedUnits page = await new CondominiumUnits(db).ListAsync(new UnitFilter(null, null, UnitStatusFilter.All, null), 1, 50, Ct);

        page.Items.Select(i => $"{i.Unit.Block}|{i.Unit.Number}").Should().Equal("|Casa 2", "|Casa 10", "Bloco 2|1", "Bloco 2|5", "Bloco 10|1");
    }

    [Fact]
    public async Task The_list_filters_by_block_type_status_and_text_ignoring_case()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, "Bloco A", "101"));
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, "Bloco B", "102", UnitType.House));
        CondominiumUnit gone = UnitBuilders.Unit(condominium.Id, "Bloco A", "103");
        await AddAsync(condominium, gone);
        UnitRecord record = (await FindAsync(condominium, gone.Id))!;
        record.Unit.Deactivate(At);
        await SaveAsync(condominium, record);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumUnits units = new(db);
        async Task<string[]> Numbers(UnitFilter filter) => [.. (await units.ListAsync(filter, 1, 50, Ct)).Items.Select(i => i.Unit.Number)];

        (await Numbers(new UnitFilter(null, null, UnitStatusFilter.Active, null))).Should().Equal("101", "102");
        (await Numbers(new UnitFilter(null, null, UnitStatusFilter.Deactivated, null))).Should().Equal("103");
        (await Numbers(new UnitFilter(null, null, UnitStatusFilter.All, null))).Should().Equal("101", "103", "102");
        (await Numbers(new UnitFilter("bloco a", null, UnitStatusFilter.All, null))).Should().Equal("101", "103");
        (await Numbers(new UnitFilter(null, UnitType.House, UnitStatusFilter.All, null))).Should().Equal("102");
        (await Numbers(new UnitFilter(null, null, UnitStatusFilter.All, "BLOCO b"))).Should().Equal("102");
        (await Numbers(new UnitFilter(null, null, UnitStatusFilter.All, "03"))).Should().Equal("103");
    }

    [Fact]
    public async Task The_text_search_treats_percent_and_underscore_as_ordinary_characters()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "101"));
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "10_A"));

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        PagedUnits page = await new CondominiumUnits(db).ListAsync(new UnitFilter(null, null, UnitStatusFilter.All, "10_"), 1, 50, Ct);
        PagedUnits percent = await new CondominiumUnits(db).ListAsync(new UnitFilter(null, null, UnitStatusFilter.All, "%"), 1, 50, Ct);

        page.Items.Select(i => i.Unit.Number).Should().Equal("10_A");
        percent.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task The_sum_of_the_fractions_in_the_database_ignores_deactivated_units_and_the_unit_asked_to_be_left_out()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit a = UnitBuilders.Unit(condominium.Id, null, "1", idealFraction: 0.2m);
        CondominiumUnit b = UnitBuilders.Unit(condominium.Id, null, "2", idealFraction: 0.3m);
        CondominiumUnit gone = UnitBuilders.Unit(condominium.Id, null, "3", idealFraction: 0.4m);
        foreach (CondominiumUnit unit in new[] { a, b, gone })
        {
            await AddAsync(condominium, unit);
        }

        UnitRecord record = (await FindAsync(condominium, gone.Id))!;
        record.Unit.Deactivate(At);
        await SaveAsync(condominium, record);

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        CondominiumUnits units = new(db);
        (await units.ActiveFractionTotalAsync(null, Ct)).Should().Be(0.5m);
        (await units.ActiveFractionTotalAsync(a.Id, Ct)).Should().Be(0.3m);
        (await units.AllAsync(Ct)).Should().HaveCount(3);
    }

    // ---- Lote ----

    [Fact]
    public async Task A_batch_of_five_hundred_units_fits_in_one_transaction_with_a_single_log_entry()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit[] batch = [.. Enumerable.Range(1, 500).Select(i => UnitBuilders.Unit(condominium.Id, null, $"U{i}"))];

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Result result = await new CondominiumUnits(db).AddRangeAsync(batch, new ChangeLogEntry(condominium.Id, Guid.CreateVersion7(), ChangeKind.UnitBatch, ["count:500"], At), Ct);

        result.IsSuccess.Should().BeTrue();
        await using CondominiumDbContext reader = postgres.CreateContext(condominium.Id);
        (await reader.Units.CountAsync(Ct)).Should().Be(500);
        (await ChangeLogCountAsync(condominium.Id)).Should().Be(1);
    }

    [Fact]
    public async Task A_conflict_in_the_middle_of_a_batch_undoes_everything()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "U3"));
        CondominiumUnit[] batch = [.. Enumerable.Range(1, 5).Select(i => UnitBuilders.Unit(condominium.Id, null, $"U{i}"))];

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Result result = await new CondominiumUnits(db).AddRangeAsync(batch, new ChangeLogEntry(condominium.Id, Guid.CreateVersion7(), ChangeKind.UnitBatch, ["count:5"], At), Ct);

        result.Error.Should().Be(UnitErrors.IdentifierAlreadyExists);
        await using CondominiumDbContext reader = postgres.CreateContext(condominium.Id);
        (await reader.Units.CountAsync(Ct)).Should().Be(1);
        (await ChangeLogCountAsync(condominium.Id)).Should().Be(1); // só o do cadastro avulso do "U3"
    }

    [Fact]
    public async Task A_batch_that_passes_one_hundred_percent_together_with_the_existing_units_is_refused_whole()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "1", idealFraction: 0.7m));
        CondominiumUnit[] batch = [UnitBuilders.Unit(condominium.Id, null, "2", idealFraction: 0.2m), UnitBuilders.Unit(condominium.Id, null, "3", idealFraction: 0.2m)];

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        Result result = await new CondominiumUnits(db).AddRangeAsync(batch, new ChangeLogEntry(condominium.Id, Guid.CreateVersion7(), ChangeKind.UnitBatch, ["count:2"], At), Ct);

        result.Error.Should().Be(UnitErrors.FractionSumExceeded);
        await using CondominiumDbContext reader = postgres.CreateContext(condominium.Id);
        (await reader.Units.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task TakenAmong_returns_only_the_identifiers_that_already_exist_ignoring_case()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, "Bloco A", "101"));

        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        IReadOnlySet<UnitIdentity> taken = await new CondominiumUnits(db).TakenAmongAsync(
            [new UnitIdentity("bloco a", "101"), new UnitIdentity("Bloco A", "102"), new UnitIdentity(null, "101")], Ct);

        taken.Should().BeEquivalentTo([new UnitIdentity("Bloco A", "101")]);
    }

    // ---- Registro de alterações ----

    [Fact]
    public async Task The_change_log_is_written_in_the_same_save_with_the_kind_the_subject_and_only_field_names()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, "Bloco A", "101", idealFraction: 0.1m);
        Guid actor = Guid.CreateVersion7();
        await using (CondominiumDbContext db = postgres.CreateContext(condominium.Id))
        {
            await new CondominiumUnits(db).AddAsync(unit, new ChangeLogEntry(condominium.Id, actor, ChangeKind.Unit, ["block", "number", "idealFraction"], At, unit.Id), Ct);
        }

        await using CondominiumDbContext reader = postgres.CreateContext();
        (await reader.Database.SqlQuery<string>($"""select kind as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct)).Should().Be("unit");
        (await reader.Database.SqlQuery<string>($"""select array_to_string(fields, ',') as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct)).Should().Be("block,number,idealFraction");
        (await reader.Database.SqlQuery<Guid>($"""select subject_id as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct)).Should().Be(unit.Id);
        (await reader.Database.SqlQuery<Guid>($"""select actor_user_id as "Value" from condominium.change_log where condominium_id = {condominium.Id}""").SingleAsync(Ct)).Should().Be(actor);
    }

    [Fact]
    public async Task A_refused_creation_leaves_no_unit_and_no_log_entry()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();
        await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "101"));

        (await AddAsync(condominium, UnitBuilders.Unit(condominium.Id, null, "101"))).IsFailure.Should().BeTrue();

        await using CondominiumDbContext reader = postgres.CreateContext(condominium.Id);
        (await reader.Units.CountAsync(Ct)).Should().Be(1);
        (await ChangeLogCountAsync(condominium.Id)).Should().Be(1);
    }

    // ---- Método de rateio ----

    [Fact]
    public async Task The_apportionment_method_is_saved_with_the_version_and_a_stale_save_is_a_conflict()
    {
        CondominiumEntity condominium = await SeedCondominiumAsync();

        await using CondominiumDbContext first = postgres.CreateContext(condominium.Id);
        await using CondominiumDbContext second = postgres.CreateContext(condominium.Id);
        ApportionmentSetting setting1 = (await new ApportionmentSettings(first).FindAsync(condominium.Id, Ct))!;
        ApportionmentSetting setting2 = (await new ApportionmentSettings(second).FindAsync(condominium.Id, Ct))!;
        setting1.Condominium.ApportionmentMethod.Should().Be(ApportionmentMethod.IdealFraction);

        setting1.Condominium.ChangeApportionmentMethod(ApportionmentMethod.Area);
        Result<string> saved = await new ApportionmentSettings(first).SaveAsync(setting1, new ChangeLogEntry(condominium.Id, Guid.CreateVersion7(), ChangeKind.Apportionment, ["apportionmentMethod"], At), Ct);
        setting2.Condominium.ChangeApportionmentMethod(ApportionmentMethod.EqualShare);
        Result<string> stale = await new ApportionmentSettings(second).SaveAsync(setting2, new ChangeLogEntry(condominium.Id, Guid.CreateVersion7(), ChangeKind.Apportionment, ["apportionmentMethod"], At), Ct);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(setting1.Version);
        stale.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        await using CondominiumDbContext reader = postgres.CreateContext(condominium.Id);
        ApportionmentSetting current = (await new ApportionmentSettings(reader).FindAsync(condominium.Id, Ct))!;
        current.Condominium.ApportionmentMethod.Should().Be(ApportionmentMethod.Area);
        current.Version.Should().Be(saved.Value);
    }

    // ---- Apoio ----

    private async Task<CondominiumEntity> SeedCondominiumAsync()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person manager = CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy);
        await using CondominiumDbContext db = postgres.CreateContext();
        (await new CondominiumRegistrations(db).SaveAsync(condominium, manager, Ct)).IsSuccess.Should().BeTrue();

        return condominium;
    }

    private static ChangeLogEntry Entry(CondominiumEntity condominium, Guid unitId, params string[] fields) =>
        new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, fields, At, unitId);

    private async Task<Result<string>> AddAsync(CondominiumEntity condominium, CondominiumUnit unit)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumUnits(db).AddAsync(unit, Entry(condominium, unit.Id, "number"), Ct);
    }

    private async Task<UnitRecord?> FindAsync(CondominiumEntity condominium, Guid unitId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);

        return await new CondominiumUnits(db).FindAsync(unitId, Ct);
    }

    private async Task SaveAsync(CondominiumEntity condominium, UnitRecord record)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        // Lê de novo no contexto da gravação, aplica a mesma mudança e grava.
        UnitRecord fresh = (await new CondominiumUnits(db).FindAsync(record.Unit.Id, Ct))!;
        if (!record.Unit.IsActive)
        {
            fresh.Unit.Deactivate(record.Unit.DeactivatedAt!.Value);
        }

        (await new CondominiumUnits(db).SaveAsync(fresh, Entry(condominium, record.Unit.Id, "deactivated"), Ct)).IsSuccess.Should().BeTrue();
    }

    private async Task<int> ChangeLogCountAsync(Guid condominiumId)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        return await db.Database.SqlQuery<int>($"""select count(*)::int as "Value" from condominium.change_log where condominium_id = {condominiumId}""").SingleAsync(Ct);
    }

    private static async Task<PrecisionInfo> Precision(CondominiumDbContext db, string column) =>
        await db.Database.SqlQuery<PrecisionInfo>($"""
            select numeric_precision as precision, numeric_scale as scale from information_schema.columns
            where table_schema = 'condominium' and table_name = 'unit' and column_name = {column}
            """).SingleAsync(Ct);

    private sealed record ColumnInfo(string DataType, int? Length);

    private sealed record PrecisionInfo(int Precision, int Scale);
}
