using Microsoft.EntityFrameworkCore;
using Urban.SharedKernel.Contracts;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// O nome dos condomínios para os outros módulos. A tabela <c>condominium</c> é a raiz do tenant e fica fora do filtro,
/// então a consulta enxerga qualquer condomínio, seja qual for o tenant da requisição (o administrador não tem um).
/// </summary>
public sealed class CondominiumDirectory(CondominiumDbContext db) : ICondominiumDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> condominiumIds, CancellationToken cancellationToken)
    {
        if (condominiumIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        Guid[] ids = [.. condominiumIds];

        return await db.Condominiums
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }
}
