using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// O lado do Condomínio do contrato de moradia: em que unidades uma conta mora hoje. Vale só o vínculo em vigor das pessoas ligadas à
/// conta no condomínio da requisição (conferido aqui de novo além do filtro global); o proprietário e o inquilino têm unidade para
/// cobrar, o dependente não.
/// </summary>
public sealed class ResidentHousing(CondominiumDbContext db, ITenantContext tenant) : IResidentHousing
{
    public async Task<ResidentDwelling> GetAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return new ResidentDwelling([], false);
        }

        // Uma consulta só: os vínculos em vigor das pessoas desta conta, cada um com a sua unidade.
        var rows = await db.Occupancies
            .AsNoTracking()
            .Where(occupancy => occupancy.CondominiumId == condominiumId
                && occupancy.StartDate <= today
                && (occupancy.EndDate == null || occupancy.EndDate >= today))
            .Join(
                db.People.Where(person => person.CondominiumId == condominiumId && person.UserId == userId),
                occupancy => occupancy.PersonId,
                person => person.Id,
                (occupancy, _) => occupancy)
            .Join(
                db.Units,
                occupancy => occupancy.UnitId,
                unit => unit.Id,
                (occupancy, unit) => new { occupancy.Kind, Unit = unit })
            .ToListAsync(cancellationToken);

        List<CondominiumUnit> units = [.. rows.Where(row => row.Kind != OccupancyKind.Dependent).Select(row => row.Unit).DistinctBy(unit => unit.Id)];
        units.Sort(UnitOrder.Comparer);

        return new ResidentDwelling(
            [.. units.Select(unit => new ResidentUnit(unit.Id, unit.Block, unit.Number))],
            rows.Any(row => row.Kind == OccupancyKind.Dependent));
    }
}
