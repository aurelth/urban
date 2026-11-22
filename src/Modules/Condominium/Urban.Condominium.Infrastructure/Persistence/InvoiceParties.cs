using Microsoft.EntityFrameworkCore;
using Urban.Condominium.Domain.Occupancies;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// O lado do Condomínio do contrato da fatura. O condomínio e os vínculos são sempre os do tenant da requisição (ou da tarefa em
/// segundo plano que carrega o tenant), conferido aqui de novo além do filtro global.
/// </summary>
public sealed class InvoiceParties(CondominiumDbContext db, ITenantContext tenant, IManagerProfile managers) : IInvoiceParties
{
    public async Task<InvoiceCondominium?> GetCondominiumAsync(CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return null;
        }

        CondominiumEntity? condominium = await db.Condominiums.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == condominiumId, cancellationToken);

        if (condominium is null)
        {
            return null;
        }

        var manager = await db.People
            .AsNoTracking()
            .Where(person => person.CondominiumId == condominiumId && person.UserId == condominium.CreatedBy)
            .Select(person => new { person.FullName, person.Phone })
            .FirstOrDefaultAsync(cancellationToken);

        return new InvoiceCondominium(
            condominium.Name,
            condominium.Cnpj.Formatted,
            condominium.Address.Formatted,
            manager is null ? null : new InvoiceManager(manager.FullName.Value, manager.Phone.Formatted));
    }

    public async Task<IReadOnlyDictionary<Guid, InvoiceResponsible>> GetResponsiblesAsync(
        IReadOnlyCollection<Guid> unitIds, DateOnly today, CancellationToken cancellationToken)
    {
        if (unitIds.Count == 0 || tenant.CondominiumId is not { } condominiumId)
        {
            return new Dictionary<Guid, InvoiceResponsible>();
        }

        _ = managers;
        Guid[] ids = [.. unitIds];

        // Uma consulta só para todas as unidades; quem escolhe entre os vínculos é a regra pura do Domain, em memória.
        var rows = await db.Occupancies
            .AsNoTracking()
            .Where(occupancy => occupancy.CondominiumId == condominiumId
                && ids.Contains(occupancy.UnitId)
                && occupancy.Kind != OccupancyKind.Dependent
                && occupancy.StartDate <= today
                && (occupancy.EndDate == null || occupancy.EndDate >= today))
            .Join(
                db.People,
                occupancy => occupancy.PersonId,
                person => person.Id,
                (occupancy, person) => new { occupancy.UnitId, occupancy.PersonId, occupancy.Kind, occupancy.StartDate, occupancy.EndDate, person.FullName, person.Email })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, InvoiceResponsible> responsibles = [];

        foreach (var unit in rows.GroupBy(row => row.UnitId))
        {
            Guid? chosen = ChargeResponsible.Choose(unit.Select(row => new ResponsibleCandidate(row.PersonId, row.Kind, row.StartDate, row.EndDate)), today);

            if (chosen is { } personId)
            {
                var row = unit.First(candidate => candidate.PersonId == personId);
                responsibles[unit.Key] = new InvoiceResponsible(row.FullName.Value, row.Email?.Value);
            }
        }

        return responsibles;
    }
}
