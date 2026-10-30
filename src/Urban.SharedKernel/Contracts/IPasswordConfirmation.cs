namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Contrato do módulo Identity para os outros módulos: confirma a senha de quem já está logado antes de uma ação
/// sensível. A tentativa errada conta para o bloqueio da conta, igual a um login.
/// </summary>
public interface IPasswordConfirmation
{
    /// <returns>
    /// Sucesso, ou a falha de negócio: senha incorreta, conta bloqueada por tentativas, conta sem acesso ou sessão inválida.
    /// </returns>
    Task<Result> ConfirmAsync(Guid userId, string? password, CancellationToken cancellationToken);
}
