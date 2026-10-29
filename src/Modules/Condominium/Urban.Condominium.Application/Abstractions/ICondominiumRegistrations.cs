using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>Porta de persistência do cadastro de condomínio. Vale para a plataforma inteira, não só para um tenant.</summary>
public interface ICondominiumRegistrations
{
    Task<bool> CnpjExistsAsync(Cnpj cnpj, CancellationToken cancellationToken);

    Task<bool> ManagerHasCondominiumAsync(Guid managerUserId, CancellationToken cancellationToken);

    /// <summary>Grava o condomínio e o síndico juntos. Os conflitos de negócio que só o banco enxerga (concorrência) voltam como falha.</summary>
    Task<Result> SaveAsync(CondominiumEntity condominium, Person manager, CancellationToken cancellationToken);
}
