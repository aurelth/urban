using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// O lado do Condomínio do contrato dos comunicados: blocos, unidades e quem mora onde. Tudo é do condomínio da requisição (conferido
/// aqui de novo além do filtro global). Valem só os vínculos em vigor hoje, de qualquer papel, e só as pessoas com conta.
/// </summary>
public sealed class AnnouncementAudienceDirectory(CondominiumDbContext db, ITenantContext tenant) : IAnnouncementAudience
{
    public async Task<bool> BlockExistsAsync(string block, CancellationToken cancellationToken) =>
        tenant.CondominiumId is { } condominiumId
        && await db.Units.AsNoTracking().AnyAsync(unit => unit.CondominiumId == condominiumId && unit.Block == block, cancellationToken);

    public async Task<ResidentUnit?> FindUnitAsync(Guid unitId, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return null;
        }

        CondominiumUnit? unit = await db.Units.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.CondominiumId == condominiumId && candidate.Id == unitId, cancellationToken);

        return unit is null ? null : new ResidentUnit(unit.Id, unit.Block, unit.Number);
    }

    public async Task<IReadOnlyList<ResidentUnit>> GetUnitsAsync(Guid userId, DateOnly today, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return [];
        }

        // Uma consulta só: os vínculos em vigor das pessoas desta conta, cada um com a sua unidade.
        List<CondominiumUnit> units = await db.Occupancies
            .AsNoTracking()
            .Where(occupancy => occupancy.CondominiumId == condominiumId
                && occupancy.StartDate <= today
                && (occupancy.EndDate == null || occupancy.EndDate >= today))
            .Join(
                db.People.Where(person => person.CondominiumId == condominiumId && person.UserId == userId),
                occupancy => occupancy.PersonId,
                person => person.Id,
                (occupancy, _) => occupancy)
            .Join(db.Units, occupancy => occupancy.UnitId, unit => unit.Id, (_, unit) => unit)
            .ToListAsync(cancellationToken);

        units = [.. units.DistinctBy(unit => unit.Id)];
        units.Sort(UnitOrder.Comparer);

        return [.. units.Select(unit => new ResidentUnit(unit.Id, unit.Block, unit.Number))];
    }

    public async Task<IReadOnlyList<IReadOnlySet<Guid>>> GetRecipientsAsync(IReadOnlyList<AudienceTarget> targets, DateOnly today, CancellationToken cancellationToken)
    {
        if (targets.Count == 0 || tenant.CondominiumId is not { } condominiumId)
        {
            return [.. targets.Select(_ => (IReadOnlySet<Guid>)new HashSet<Guid>())];
        }

        // Uma consulta para todos os alcances: quem tem conta e mora hoje em cada unidade. Quem escolhe o alcance é a memória.
        var residents = await db.Occupancies
            .AsNoTracking()
            .Where(occupancy => occupancy.CondominiumId == condominiumId
                && occupancy.StartDate <= today
                && (occupancy.EndDate == null || occupancy.EndDate >= today))
            .Join(
                db.People.Where(person => person.CondominiumId == condominiumId && person.UserId != null),
                occupancy => occupancy.PersonId,
                person => person.Id,
                (occupancy, person) => new { occupancy.UnitId, UserId = person.UserId!.Value })
            .Join(db.Units, resident => resident.UnitId, unit => unit.Id, (resident, unit) => new { resident.UserId, UnitId = unit.Id, unit.Block })
            .ToListAsync(cancellationToken);

        return [.. targets.Select(target => (IReadOnlySet<Guid>)residents.Where(resident => Reaches(target, resident.UnitId, resident.Block)).Select(resident => resident.UserId).ToHashSet())];
    }

    private static bool Reaches(AudienceTarget target, Guid unitId, string? block) =>
        target.UnitId is { } unit ? unitId == unit : target.Block is not { } wanted || block == wanted;
}
