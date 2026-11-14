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
}
