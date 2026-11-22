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
                (occupancy, person) => new { occupancy.UnitId, occupancy.PersonId, occupancy.Kind, occupancy.StartDate, occupancy.EndDate, person.FullName, person.Email, person.UserId })
            .ToListAsync(cancellationToken);

        var chosen = new List<(Guid UnitId, string Name, string? Email, Guid? UserId)>();

        foreach (var unit in rows.GroupBy(row => row.UnitId))
        {
            Guid? personId = ChargeResponsible.Choose(unit.Select(row => new ResponsibleCandidate(row.PersonId, row.Kind, row.StartDate, row.EndDate)), today);

            if (personId is { } id)
            {
                var row = unit.First(candidate => candidate.PersonId == id);
                chosen.Add((unit.Key, row.FullName.Value, row.Email?.Value, row.UserId));
            }
        }

        IReadOnlyDictionary<Guid, string> accountEmails = await AccountEmailsAsync(condominiumId, chosen, cancellationToken);

        return chosen.ToDictionary(
            found => found.UnitId,
            found => new InvoiceResponsible(found.Name, found.Email ?? (found.UserId is { } userId ? accountEmails.GetValueOrDefault(userId) : null)));
    }

    /// <summary>
    /// O síndico é a única pessoa com conta e sem e-mail no cadastro (o do morador nasce junto com o convite), e o endereço dele é o do
    /// login. Só quem ficou sem e-mail é perguntado, e cada conta uma vez, mesmo que ele seja o responsável de várias unidades.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> AccountEmailsAsync(
        Guid condominiumId, IEnumerable<(Guid UnitId, string Name, string? Email, Guid? UserId)> chosen, CancellationToken cancellationToken)
    {
        Dictionary<Guid, string> emails = [];

        foreach (Guid userId in chosen.Where(found => found.Email is null && found.UserId is not null).Select(found => found.UserId!.Value).Distinct())
        {
            if (await managers.GetEmailAsync(userId, condominiumId, cancellationToken) is { } email)
            {
                emails[userId] = email;
            }
        }

        return emails;
    }
}
