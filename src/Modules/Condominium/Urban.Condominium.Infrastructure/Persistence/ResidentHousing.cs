using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>O lado do Condomínio do contrato de moradia: em que unidades uma conta mora hoje.</summary>
public sealed class ResidentHousing : IResidentHousing
{
    public ResidentHousing(CondominiumDbContext db, ITenantContext tenant)
    {
        _ = db;
        _ = tenant;
    }

    public Task<ResidentDwelling> GetAsync(Guid userId, DateOnly today, CancellationToken cancellationToken) =>
        Task.FromResult(new ResidentDwelling([], false));
}
