namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Contrato do módulo Identity para os outros módulos: o nome pelo qual o síndico prefere ser chamado mora na conta
/// dele, então quem edita o perfil lê e grava por aqui, sem conhecer as camadas internas do Identity.
/// </summary>
public interface IManagerProfile
{
    /// <returns>O nome de exibição atual, ou nulo quando a conta não existe ou ainda não tem um.</returns>
    Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// O e-mail do login do síndico daquele condomínio. É o endereço de quem tem conta mas nenhum e-mail no cadastro de pessoa (o
    /// síndico), e só vale para a conta de síndico do próprio condomínio: conta de outro papel ou de outro condomínio não o revela.
    /// </summary>
    /// <returns>O e-mail da conta, ou nulo quando a conta não existe, não é de síndico ou é de outro condomínio.</returns>
    Task<string?> GetEmailAsync(Guid userId, Guid condominiumId, CancellationToken cancellationToken);

    /// <summary>Grava o nome de exibição. Não confirma a transação: quem chama decide, dentro da unidade atômica dele.</summary>
    /// <returns>O nome já normalizado, ou a falha de negócio (nome inválido, conta sem acesso, síndico sem condomínio).</returns>
    Task<Result<string>> UpdateDisplayNameAsync(Guid userId, string? displayName, CancellationToken cancellationToken);
}
