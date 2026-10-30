namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Contrato do módulo Identity para os outros módulos: o nome pelo qual o síndico prefere ser chamado mora na conta
/// dele, então quem edita o perfil lê e grava por aqui, sem conhecer as camadas internas do Identity.
/// </summary>
public interface IManagerProfile
{
    /// <returns>O nome de exibição atual, ou nulo quando a conta não existe ou ainda não tem um.</returns>
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Grava o nome de exibição. Não confirma a transação: quem chama decide, dentro da unidade atômica dele.</summary>
    /// <returns>O nome já normalizado, ou a falha de negócio (nome inválido, conta sem acesso, síndico sem condomínio).</returns>
    Task<Result<string>> UpdateDisplayNameAsync(Guid userId, string? displayName, CancellationToken cancellationToken);
}
