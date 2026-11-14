using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>Os veículos no PostgreSQL de verdade: índice único da placa, versão, tenant, lista com filtros, exclusão e registro de alterações.</summary>
[Collection(CondominiumPostgresCollection.Name)]
public sealed class CondominiumVehiclesTests(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateTimeOffset At = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);

    // ---- Esquema e migration ----

    [Fact]
    public async Task The_migration_creates_the_vehicle_table_with_a_unique_index_on_the_plate_per_condominium()
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        List<string> tables = await db.Database
            .SqlQuery<string>($"""select table_name as "Value" from information_schema.tables where table_schema = 'condominium'""")
            .ToListAsync(Ct);
        string index = await db.Database
            .SqlQuery<string>($"""select indexdef as "Value" from pg_indexes where schemaname = 'condominium' and indexname = 'ix_vehicle_plate'""")
            .SingleAsync(Ct);

        tables.Should().Contain("vehicle");
        index.Should().Contain("UNIQUE").And.Contain("condominium_id").And.Contain("plate");
    }

    [Theory]
    [InlineData("plate", 7)]
    [InlineData("model", 60)]
    [InlineData("color", 30)]
    public async Task The_columns_have_the_length_of_the_domain_rules(string column, int length)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        int? actual = await db.Database.SqlQuery<int?>($"""
            select character_maximum_length as "Value" from information_schema.columns
            where table_schema = 'condominium' and table_name = 'vehicle' and column_name = {column}
            """).SingleAsync(Ct);

        actual.Should().Be(length);
    }

    // ---- Placa única ----

    [Fact]
    public async Task The_plate_is_unique_per_condominium_and_the_same_plate_can_exist_in_another()
    {
        Seed first = await SeedAsync();
        Seed second = await SeedAsync();
        await AddAsync(first, "ABC1D23");

        Result<string> repeated = await AddAsync(first, "ABC1D23");
        Result<string> otherCondominium = await AddAsync(second, "ABC1D23");

        repeated.Error.Should().Be(VehicleErrors.PlateAlreadyRegistered);
        otherCondominium.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task A_rejected_registration_writes_no_change_log()
    {
        Seed seed = await SeedAsync();
        await AddAsync(seed, "ABC1D23");
        int before = await ChangeLogCountAsync(seed.Condominium.Id);

        (await AddAsync(seed, "ABC1D23")).IsFailure.Should().BeTrue();

        (await ChangeLogCountAsync(seed.Condominium.Id)).Should().Be(before);
    }

    [Fact]
    public async Task Two_simultaneous_registrations_of_the_same_plate_let_one_through_and_give_the_other_a_conflict_never_an_exception()
    {
        Seed seed = await SeedAsync();

        Result<string>[] results = await Task.WhenAll(AddAsync(seed, "QWE1R23"), AddAsync(seed, "QWE1R23"));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error.Should().Be(VehicleErrors.PlateAlreadyRegistered);
    }

    [Fact]
    public async Task IsPlateTaken_ignoresTheVehicleBeingEdited()
    {
        Seed seed = await SeedAsync();
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "ABC1234");
        await AddAsync(seed, vehicle);

        await using CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id);
        CondominiumVehicles vehicles = new(db);
        (await vehicles.IsPlateTakenAsync("ABC1234", null, Ct)).Should().BeTrue();
        (await vehicles.IsPlateTakenAsync("ABC1234", vehicle.Id, Ct)).Should().BeFalse();
        (await vehicles.IsPlateTakenAsync("ZZZ9999", null, Ct)).Should().BeFalse();
    }

    // ---- Tenant, versão e valores ----

    [Fact]
    public async Task A_condominium_sees_only_its_own_vehicles_and_without_a_tenant_nothing_is_visible()
    {
        Seed a = await SeedAsync();
        Seed b = await SeedAsync();
        Vehicle vehicleA = VehicleBuilders.Build(a.Condominium.Id, a.Unit.Id, "AAA1A11");
        Vehicle vehicleB = VehicleBuilders.Build(b.Condominium.Id, b.Unit.Id, "BBB2B22");
        await AddAsync(a, vehicleA);
        await AddAsync(b, vehicleB);

        await using CondominiumDbContext asA = postgres.CreateContext(a.Condominium.Id);
        await using CondominiumDbContext withoutTenant = postgres.CreateContext(null);
        CondominiumVehicles vehicles = new(asA);

        (await vehicles.FindAsync(vehicleA.Id, Ct)).Should().NotBeNull();
        (await vehicles.FindAsync(vehicleB.Id, Ct)).Should().BeNull();
        (await vehicles.ListAsync(new VehicleFilter(null, null), 1, 200, Ct)).Items.Select(i => i.Vehicle.Id).Should().Contain(vehicleA.Id).And.NotContain(vehicleB.Id);
        (await vehicles.IsPlateTakenAsync("BBB2B22", null, Ct)).Should().BeFalse("a placa do outro condomínio não existe para este");
        (await withoutTenant.Set<Vehicle>().CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task The_vehicle_comes_back_with_its_unit_and_the_values_it_went_in_with()
    {
        Seed seed = await SeedAsync("Bloco C", "303");
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "ABC1234", "Honda Civic", "Branco");
        await AddAsync(seed, vehicle);

        VehicleRecord read = (await FindAsync(seed, vehicle.Id))!;

        read.Vehicle.Plate.Should().Be("ABC1234");
        read.Vehicle.Model.Should().Be("Honda Civic");
        read.Vehicle.Color.Should().Be("Branco");
        read.Vehicle.UnitId.Should().Be(seed.Unit.Id);
        read.Vehicle.CreatedAt.Should().Be(VehicleBuilders.Now);
        read.UnitBlock.Should().Be("Bloco C");
        read.UnitNumber.Should().Be("303");
    }

    [Fact]
    public async Task The_version_changes_with_every_change_and_saving_with_an_old_version_is_a_conflict()
    {
        Seed seed = await SeedAsync();
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "ABC1234", "Gol", "Preto");
        await AddAsync(seed, vehicle);

        await using CondominiumDbContext first = postgres.CreateContext(seed.Condominium.Id);
        await using CondominiumDbContext second = postgres.CreateContext(seed.Condominium.Id);
        VehicleRecord reading1 = (await new CondominiumVehicles(first).FindAsync(vehicle.Id, Ct))!;
        VehicleRecord reading2 = (await new CondominiumVehicles(second).FindAsync(vehicle.Id, Ct))!;
        reading1.Version.Should().Be(reading2.Version);

        reading1.Vehicle.Update(seed.Unit.Id, "ABC1234", "Gol", "Vermelho");
        Result<string> saved = await new CondominiumVehicles(first).SaveAsync(reading1, Entry(seed, vehicle.Id, "color"), Ct);
        reading2.Vehicle.Update(seed.Unit.Id, "ABC1234", "Gol", "Azul");
        Result<string> stale = await new CondominiumVehicles(second).SaveAsync(reading2, Entry(seed, vehicle.Id, "color"), Ct);

        saved.IsSuccess.Should().BeTrue();
        saved.Value.Should().NotBe(reading1.Version);
        stale.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        VehicleRecord current = (await FindAsync(seed, vehicle.Id))!;
        current.Version.Should().Be(saved.Value);
        current.Vehicle.Color.Should().Be("Vermelho");
    }

    [Fact]
    public async Task Changing_the_plate_to_one_that_already_exists_is_a_conflict_never_an_exception()
    {
        Seed seed = await SeedAsync();
        await AddAsync(seed, "AAA1111");
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "BBB2222");
        await AddAsync(seed, vehicle);

        await using CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id);
        VehicleRecord record = (await new CondominiumVehicles(db).FindAsync(vehicle.Id, Ct))!;
        record.Vehicle.Update(seed.Unit.Id, "AAA1111", "Fiat Argo", "Prata");

        Result<string> result = await new CondominiumVehicles(db).SaveAsync(record, Entry(seed, vehicle.Id, "plate"), Ct);

        result.Error.Should().Be(VehicleErrors.PlateAlreadyRegistered);
    }

    // ---- Exclusão ----

    [Fact]
    public async Task Deleting_removes_the_row_and_writes_one_change_log_with_the_kind_and_no_values()
    {
        Seed seed = await SeedAsync();
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "DEL1D23");
        await AddAsync(seed, vehicle);
        int before = await ChangeLogCountAsync(seed.Condominium.Id);

        await using (CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id))
        {
            VehicleRecord record = (await new CondominiumVehicles(db).FindAsync(vehicle.Id, Ct))!;
            (await new CondominiumVehicles(db).DeleteAsync(record, Entry(seed, vehicle.Id, "removed"), Ct)).IsSuccess.Should().BeTrue();
        }

        (await FindAsync(seed, vehicle.Id)).Should().BeNull();
        (await ChangeLogCountAsync(seed.Condominium.Id)).Should().Be(before + 1);
        string fields = await LastChangeLogAsync(seed.Condominium.Id);
        fields.Should().Be("vehicle:removed");
    }

    [Fact]
    public async Task The_plate_is_free_again_after_the_vehicle_is_deleted()
    {
        Seed seed = await SeedAsync();
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "REU1S23");
        await AddAsync(seed, vehicle);
        await using (CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id))
        {
            VehicleRecord record = (await new CondominiumVehicles(db).FindAsync(vehicle.Id, Ct))!;
            await new CondominiumVehicles(db).DeleteAsync(record, Entry(seed, vehicle.Id, "removed"), Ct);
        }

        (await AddAsync(seed, "REU1S23")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_a_vehicle_that_changed_in_the_meantime_is_a_conflict_and_the_row_stays()
    {
        Seed seed = await SeedAsync();
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "CON1F23");
        await AddAsync(seed, vehicle);

        await using CondominiumDbContext reader = postgres.CreateContext(seed.Condominium.Id);
        VehicleRecord stale = (await new CondominiumVehicles(reader).FindAsync(vehicle.Id, Ct))!;
        await using (CondominiumDbContext writer = postgres.CreateContext(seed.Condominium.Id))
        {
            VehicleRecord fresh = (await new CondominiumVehicles(writer).FindAsync(vehicle.Id, Ct))!;
            fresh.Vehicle.Update(seed.Unit.Id, "CON1F23", "Fiat Argo", "Verde");
            await new CondominiumVehicles(writer).SaveAsync(fresh, Entry(seed, vehicle.Id, "color"), Ct);
        }

        Result result = await new CondominiumVehicles(reader).DeleteAsync(stale, Entry(seed, vehicle.Id, "removed"), Ct);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        (await FindAsync(seed, vehicle.Id)).Should().NotBeNull();
    }

    // ---- Lista ----

    [Fact]
    public async Task The_list_is_ordered_by_unit_in_natural_order_and_then_by_plate()
    {
        Seed seed = await SeedAsync("Bloco A", "10");
        CondominiumUnit two = await AddUnitAsync(seed, "Bloco A", "2");
        CondominiumUnit noBlock = await AddUnitAsync(seed, null, "5");
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "CCC3333"));
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, two.Id, "BBB2222"));
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, two.Id, "AAA1111"));
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, noBlock.Id, "DDD4444"));

        PagedVehicles page = await ListAsync(seed, new VehicleFilter(null, null), 1, 50);

        page.Total.Should().Be(4);
        page.Items.Select(i => i.Vehicle.Plate).Should().Equal("DDD4444", "AAA1111", "BBB2222", "CCC3333");
    }

    [Fact]
    public async Task The_list_filters_by_unit()
    {
        Seed seed = await SeedAsync("Bloco A", "101");
        CondominiumUnit other = await AddUnitAsync(seed, "Bloco A", "102");
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "AAA1111"));
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, other.Id, "BBB2222"));

        PagedVehicles page = await ListAsync(seed, new VehicleFilter(other.Id, null), 1, 50);

        page.Total.Should().Be(1);
        page.Items.Single().Vehicle.Plate.Should().Be("BBB2222");
        page.Items.Single().UnitNumber.Should().Be("102");
    }

    [Theory]
    [InlineData("abc-12", "ABC1234")]
    [InlineData("ABC 1D", "ABC1D23")]
    [InlineData("civic", "CIV1C23")]
    [InlineData("PRATA", "PRA1T23")]
    public async Task The_search_finds_the_plate_ignoring_case_and_separators_the_model_and_the_color(string search, string expectedPlate)
    {
        Seed seed = await SeedAsync();
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "ABC1234", "Gol", "Preto"));
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "ABC1D23", "Fiat Argo", "Vermelho"));
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "CIV1C23", "Honda Civic", "Branco"));
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "PRA1T23", "Uno", "Prata"));

        PagedVehicles page = await ListAsync(seed, new VehicleFilter(null, search), 1, 50);

        page.Items.Select(i => i.Vehicle.Plate).Should().Equal(expectedPlate);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task The_search_treats_like_wildcards_as_plain_characters(string search)
    {
        Seed seed = await SeedAsync();
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "ABC1234", "Gol", "Preto"));

        (await ListAsync(seed, new VehicleFilter(null, search), 1, 50)).Total.Should().Be(0);
    }

    [Fact]
    public async Task A_search_made_only_of_separators_does_not_filter_by_plate_and_does_not_break()
    {
        Seed seed = await SeedAsync();
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "ABC1234", "Gol", "Preto"));

        (await ListAsync(seed, new VehicleFilter(null, "--"), 1, 50)).Total.Should().Be(0);
    }

    [Fact]
    public async Task The_list_pages_in_the_database_and_reports_the_total()
    {
        Seed seed = await SeedAsync();
        foreach (string plate in new[] { "AAA1111", "BBB2222", "CCC3333", "DDD4444", "EEE5555" })
        {
            await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, plate));
        }

        PagedVehicles second = await ListAsync(seed, new VehicleFilter(null, null), 2, 2);

        second.Total.Should().Be(5);
        second.Items.Select(i => i.Vehicle.Plate).Should().Equal("CCC3333", "DDD4444");
    }

    // ---- Registro de alterações ----

    [Fact]
    public async Task Registering_writes_the_vehicle_and_the_change_log_in_one_save()
    {
        Seed seed = await SeedAsync();
        Vehicle vehicle = VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, "LOG1G23");

        (await AddAsync(seed, vehicle)).IsSuccess.Should().BeTrue();

        (await LastChangeLogAsync(seed.Condominium.Id)).Should().Be("vehicle:unit,plate,model,color");
    }

    // ---- Apoio ----

    private sealed record Seed(CondominiumEntity Condominium, CondominiumUnit Unit);

    private async Task<Seed> SeedAsync(string? block = "Bloco A", string number = "101")
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person manager = CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy);
        await using (CondominiumDbContext db = postgres.CreateContext())
        {
            (await new CondominiumRegistrations(db).SaveAsync(condominium, manager, Ct)).IsSuccess.Should().BeTrue();
        }

        Seed seed = new(condominium, UnitBuilders.Unit(condominium.Id, block, number));
        await using CondominiumDbContext units = postgres.CreateContext(condominium.Id);
        (await new CondominiumUnits(units).AddAsync(seed.Unit, new ChangeLogEntry(condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, ["number"], At, seed.Unit.Id), Ct)).IsSuccess.Should().BeTrue();

        return seed;
    }

    private async Task<CondominiumUnit> AddUnitAsync(Seed seed, string? block, string number)
    {
        CondominiumUnit unit = UnitBuilders.Unit(seed.Condominium.Id, block, number);
        await using CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id);
        (await new CondominiumUnits(db).AddAsync(unit, new ChangeLogEntry(seed.Condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, ["number"], At, unit.Id), Ct)).IsSuccess.Should().BeTrue();

        return unit;
    }

    private static ChangeLogEntry Entry(Seed seed, Guid vehicleId, params string[] fields) =>
        new(seed.Condominium.Id, Guid.CreateVersion7(), ChangeKind.Vehicle, fields, At, vehicleId);

    private async Task<Result<string>> AddAsync(Seed seed, string plate) =>
        await AddAsync(seed, VehicleBuilders.Build(seed.Condominium.Id, seed.Unit.Id, plate));

    private async Task<Result<string>> AddAsync(Seed seed, Vehicle vehicle)
    {
        await using CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id);

        return await new CondominiumVehicles(db).AddAsync(vehicle, Entry(seed, vehicle.Id, "unit", "plate", "model", "color"), Ct);
    }

    private async Task<VehicleRecord?> FindAsync(Seed seed, Guid vehicleId)
    {
        await using CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id);

        return await new CondominiumVehicles(db).FindAsync(vehicleId, Ct);
    }

    private async Task<PagedVehicles> ListAsync(Seed seed, VehicleFilter filter, int page, int pageSize)
    {
        await using CondominiumDbContext db = postgres.CreateContext(seed.Condominium.Id);

        return await new CondominiumVehicles(db).ListAsync(filter, page, pageSize, Ct);
    }

    private async Task<int> ChangeLogCountAsync(Guid condominiumId)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        return await db.Database.SqlQuery<int>($"""select count(*)::int as "Value" from condominium.change_log where condominium_id = {condominiumId}""").SingleAsync(Ct);
    }

    /// <summary>O último registro do condomínio como "tipo:campos".</summary>
    private async Task<string> LastChangeLogAsync(Guid condominiumId)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        return await db.Database.SqlQuery<string>($"""
            select kind || ':' || array_to_string(fields, ',') as "Value" from condominium.change_log
            where condominium_id = {condominiumId} order by changed_at desc, id desc limit 1
            """).SingleAsync(Ct);
    }
}
