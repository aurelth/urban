namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Contrato do módulo Identity para quem precisa escrever à pessoa por e-mail sem conhecer contas (os comunicados). Só devolve o
/// endereço de uma conta que hoje pode entrar no sistema.
/// </summary>
public interface IAccountEmails
{
    /// <summary>
    /// O e-mail de login de uma conta de morador ou de síndico do condomínio, se ela está ativa. Conta que não existe, que é de outro
    /// condomínio, ainda não escolheu a senha, foi desativada, suspensa ou excluída não tem endereço aqui: nada deve ser enviado a ela.
    /// </summary>
    Task<string?> FindActiveEmailAsync(Guid userId, Guid condominiumId, CancellationToken cancellationToken);
}
