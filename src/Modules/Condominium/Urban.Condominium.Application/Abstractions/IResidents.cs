using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>
/// Porta de persistência dos moradores, sempre dentro do condomínio da requisição (tenant). Morador é a pessoa que tem ao menos
/// um vínculo com uma unidade: o síndico sem unidade não aparece.
/// </summary>
public interface IResidents
{
    Task<ResidentRecord?> FindAsync(Guid personId, CancellationToken cancellationToken);

    /// <summary>Uma página em ordem de nome, com todos os vínculos de cada morador.</summary>
    Task<PagedResidents> ListAsync(ResidentFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Grava a pessoa, o primeiro vínculo e o registro, numa gravação só.</summary>
    /// <returns>A versão da pessoa, ou a falha: CPF que já é de outra pessoa do condomínio.</returns>
    Task<Result<string>> AddAsync(Person person, Occupancy occupancy, ChangeLogEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Grava a edição da pessoa (que o handler já aplicou à entidade lida) e o registro, numa gravação só.
    /// </summary>
    /// <returns>A nova versão, ou a falha: versão velha (outra pessoa editou antes) ou CPF que já é de outra pessoa do condomínio.</returns>
    Task<Result<string>> SaveAsync(ResidentRecord record, ChangeLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Grava um novo vínculo de uma pessoa que já existe, com o registro. Falha se ela já mora na unidade.</summary>
    Task<Result> AddOccupancyAsync(Occupancy occupancy, ChangeLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Grava a data de saída de um vínculo ainda aberto, com o registro. Falha se outro pedido já o encerrou.</summary>
    Task<Result> EndOccupancyAsync(Occupancy occupancy, ChangeLogEntry entry, CancellationToken cancellationToken);
}
