using Urban.SharedKernel;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>Porta de persistência do método de rateio, que mora na linha do condomínio.</summary>
public interface IApportionments
{
    /// <returns>O condomínio e a versão dele, ou nulo quando não existe.</returns>
    Task<ApportionmentSetting?> FindAsync(Guid condominiumId, CancellationToken cancellationToken);

    /// <summary>Grava o método e o registro da alteração numa gravação só. Se outra pessoa gravou antes, nada é gravado.</summary>
    /// <returns>A nova versão, ou o conflito de versão.</returns>
    Task<Result<string>> SaveAsync(ApportionmentSetting setting, ChangeLogEntry entry, CancellationToken cancellationToken);
}
