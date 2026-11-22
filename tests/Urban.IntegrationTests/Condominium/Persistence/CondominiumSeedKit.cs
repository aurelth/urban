using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.IntegrationTests.Condominium.Persistence;

/// <summary>Condomínios, unidades, moradores e contas de verdade no PostgreSQL, cada passo numa "requisição" (contexto) nova, para os testes lerem o que o cadastro gravou.</summary>
internal sealed class CondominiumSeedKit(CondominiumPostgresFixture postgres)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    public async Task<CondominiumEntity> CondominiumAsync()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        Person manager = CondominiumBuilders.Manager(condominium.Id, condominium.CreatedBy);
        await using CondominiumDbContext db = postgres.CreateContext();
        (await new CondominiumRegistrations(db).SaveAsync(condominium, manager, Ct)).IsSuccess.Should().BeTrue();

        return condominium;
    }

    public async Task<CondominiumUnit> UnitAsync(CondominiumEntity condominium, string? block, string number)
    {
        CondominiumUnit unit = UnitBuilders.Unit(condominium.Id, block, number);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Unit, ["number"], UnitBuilders.Now, unit.Id);
        (await new CondominiumUnits(db).AddAsync(unit, entry, Ct)).IsSuccess.Should().BeTrue();

        return unit;
    }

    /// <summary>Cadastra a pessoa na unidade, sem acesso ao sistema (ainda sem conta).</summary>
    public async Task<Person> PersonAsync(CondominiumEntity condominium, CondominiumUnit unit, OccupancyKind kind)
    {
        Person person = ResidentBuilders.Resident(condominium.Id);
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        ChangeLogEntry entry = new(condominium.Id, Guid.CreateVersion7(), ChangeKind.Resident, ["fullName", "cpf", "occupancy"], ResidentBuilders.Now, person.Id);
        (await new CondominiumResidents(db).AddAsync(person, ResidentBuilders.Occupancy(person, unit.Id, kind), entry, Ct)).IsSuccess.Should().BeTrue();

        return person;
    }

    /// <summary>Cadastra a pessoa na unidade e liga a ela uma conta de acesso, como o convite aceito faz.</summary>
    public async Task<Guid> AccountAsync(CondominiumEntity condominium, CondominiumUnit unit, OccupancyKind kind) =>
        (await LinkedAsync(condominium, unit, kind)).Account;

    /// <summary>O mesmo que <see cref="AccountAsync"/>, devolvendo também a pessoa, para quem precisa mexer nas datas do vínculo.</summary>
    public async Task<(Guid Account, Guid PersonId)> LinkedAsync(CondominiumEntity condominium, CondominiumUnit unit, OccupancyKind kind)
    {
        Person person = await PersonAsync(condominium, unit, kind);
        Guid account = Guid.CreateVersion7();
        await LinkAsync(person.Id, account);

        return (account, person.Id);
    }

    /// <summary>Mais um vínculo da mesma pessoa, inserido direto no banco (o cadastro só cria o primeiro).</summary>
    public async Task AddOccupancyAsync(CondominiumEntity condominium, Person person, CondominiumUnit unit, OccupancyKind kind)
    {
        await using CondominiumDbContext db = postgres.CreateContext(condominium.Id);
        db.Occupancies.Add(ResidentBuilders.Occupancy(person, unit.Id, kind));
        await db.SaveChangesAsync(Ct);
    }

    public async Task LinkAsync(Guid personId, Guid userId)
    {
        await using CondominiumDbContext db = postgres.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"update condominium.person set user_id = {userId} where id = {personId}", Ct);
    }

    public async Task SetDatesAsync(Guid personId, Guid unitId, DateOnly? start = null, DateOnly? end = null)
    {
        await using CondominiumDbContext db = postgres.CreateContext();

        if (start is { } startDate)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"update condominium.occupancy set start_date = {startDate} where person_id = {personId} and unit_id = {unitId}", Ct);
        }

        if (end is { } endDate)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"update condominium.occupancy set end_date = {endDate} where person_id = {personId} and unit_id = {unitId}", Ct);
        }
    }
}
