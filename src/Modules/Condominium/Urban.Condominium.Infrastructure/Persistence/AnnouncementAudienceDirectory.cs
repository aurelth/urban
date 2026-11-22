using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>Responde ao módulo Operações quem mora em quê, só com o que o contrato <see cref="IAnnouncementAudience"/> expõe.</summary>
public sealed class AnnouncementAudienceDirectory(CondominiumDbContext db, ITenantContext tenant) : IAnnouncementAudience
{
    public Task<bool> BlockExistsAsync(string block, CancellationToken cancellationToken)
    {
        _ = (db, tenant);
        throw new NotImplementedException();
    }

    public Task<ResidentUnit?> FindUnitAsync(Guid unitId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ResidentUnit>> GetUnitsAsync(Guid userId, DateOnly today, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<IReadOnlySet<Guid>>> GetRecipientsAsync(IReadOnlyList<AudienceTarget> targets, DateOnly today, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
