using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>
/// Porta de persistência das unidades, sempre dentro do condomínio da requisição (tenant). As consultas de conferência
/// (identificador repetido, soma das frações) servem para dar uma resposta clara; quem garante a regra de verdade são
/// as gravações, que travam a linha do condomínio e conferem de novo, e o índice único do banco.
/// </summary>
public interface IUnits
{
    Task<UnitRecord?> FindAsync(Guid unitId, CancellationToken cancellationToken);

    /// <summary>Uma página na ordem natural: bloco (sem bloco primeiro) e depois número.</summary>
    Task<PagedUnits> ListAsync(UnitFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Todas as unidades, ativas e desativadas. É pouco dado (um condomínio tem de dezenas a poucas centenas).</summary>
    Task<IReadOnlyList<CondominiumUnit>> AllAsync(CancellationToken cancellationToken);

    /// <summary>Soma das frações das unidades ativas, sem contar <paramref name="exceptUnitId"/> (a unidade que está sendo editada).</summary>
    Task<decimal> ActiveFractionTotalAsync(Guid? exceptUnitId, CancellationToken cancellationToken);

    /// <summary>Se o bloco e o número já existem, em unidade ativa ou desativada, sem contar <paramref name="exceptUnitId"/>.</summary>
    Task<bool> IsIdentityTakenAsync(UnitIdentity identity, Guid? exceptUnitId, CancellationToken cancellationToken);

    /// <summary>Quais destes identificadores já existem.</summary>
    Task<IReadOnlySet<UnitIdentity>> TakenAmongAsync(IReadOnlyCollection<UnitIdentity> identities, CancellationToken cancellationToken);

    /// <returns>A versão da nova unidade, ou a falha: identificador repetido ou soma das frações acima de 100%.</returns>
    Task<Result<string>> AddAsync(CondominiumUnit unit, ChangeLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Grava as mudanças feitas na unidade lida por <see cref="FindAsync"/> junto com o registro, numa gravação só.</summary>
    /// <returns>A nova versão, ou a falha: conflito de versão, identificador repetido ou soma das frações acima de 100%.</returns>
    Task<Result<string>> SaveAsync(UnitRecord record, ChangeLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Grava o lote inteiro e um único registro, ou nada.</summary>
    Task<Result> AddRangeAsync(IReadOnlyList<CondominiumUnit> units, ChangeLogEntry entry, CancellationToken cancellationToken);
}
