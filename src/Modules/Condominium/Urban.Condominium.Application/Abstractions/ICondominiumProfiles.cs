using Urban.SharedKernel;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>Porta de persistência da leitura e da edição do cadastro do síndico, já dentro do condomínio dele (tenant).</summary>
public interface ICondominiumProfiles
{
    /// <returns>O condomínio e o síndico daquele usuário, ou nulo quando ele não tem cadastro neste tenant.</returns>
    Task<ManagerCondominium?> FindOfManagerAsync(Guid managerUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Grava as mudanças feitas nas entidades lidas por <see cref="FindOfManagerAsync"/> junto com o registro da edição,
    /// numa gravação só. Se outra pessoa gravou antes, nada é gravado.
    /// </summary>
    /// <returns>A nova versão, ou a falha de negócio: conflito de versão ou CPF que já é de outra pessoa do condomínio.</returns>
    Task<Result<string>> SaveAsync(ManagerCondominium registration, ChangeLogEntry entry, CancellationToken cancellationToken);
}
