namespace Urban.SharedKernel.Contracts;

/// <summary>
/// Contrato do módulo Identity para os outros módulos: vincula o síndico ao condomínio que ele acabou de cadastrar
/// e grava como ele prefere ser chamado. Os módulos só se conhecem por contratos como este, nunca pelas camadas internas.
/// </summary>
public interface IManagerEnrollment
{
    /// <param name="userId">Conta do síndico (vem do token de acesso).</param>
    /// <param name="condominiumId">Condomínio recém-cadastrado.</param>
    /// <param name="displayName">Como a pessoa prefere ser chamado; o Identity valida.</param>
    /// <returns>
    /// Sucesso ou a falha de negócio do Identity: conta desconhecida ou sem acesso, conta que não é de síndico,
    /// síndico que já tem condomínio, nome de exibição inválido.
    /// </returns>
    Task<Result> EnrollAsync(Guid userId, Guid condominiumId, string? displayName, CancellationToken cancellationToken);
}
