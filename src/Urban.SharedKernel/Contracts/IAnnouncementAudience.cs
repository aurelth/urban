namespace Urban.SharedKernel.Contracts;

/// <summary>
/// A quem um comunicado se dirige, pelo lado do Condomínio: o condomínio inteiro (<paramref name="Block"/> e <paramref name="UnitId"/>
/// nulos), um bloco (só <paramref name="Block"/>) ou uma unidade (só <paramref name="UnitId"/>).
/// </summary>
public sealed record AudienceTarget(string? Block, Guid? UnitId);

/// <summary>
/// Contrato do módulo Condominium para as Operações: o que os comunicados precisam saber de blocos, unidades e moradores sem conhecer
/// pessoas, vínculos nem unidades. Tudo vale para o condomínio da requisição.
/// </summary>
public interface IAnnouncementAudience
{
    /// <returns>Se o condomínio tem alguma unidade nesse bloco (o nome como foi cadastrado, sem espaços sobrando).</returns>
    Task<bool> BlockExistsAsync(string block, CancellationToken cancellationToken);

    /// <returns>A unidade, ou nulo se ela não existe neste condomínio.</returns>
    Task<ResidentUnit?> FindUnitAsync(Guid unitId, CancellationToken cancellationToken);

    /// <summary>
    /// As unidades em que a conta mora na data, em qualquer papel (proprietário, inquilino ou dependente): vínculo que já começou e ainda
    /// não terminou. Sem pessoa ligada à conta, ou sem condomínio na requisição, não há unidade.
    /// </summary>
    /// <param name="userId">A conta que entrou (o token), nunca um dado de entrada da requisição.</param>
    /// <param name="today">A data de referência (Brasília).</param>
    Task<IReadOnlyList<ResidentUnit>> GetUnitsAsync(Guid userId, DateOnly today, CancellationToken cancellationToken);

    /// <summary>
    /// As contas de quem mora hoje em cada alcance, numa consulta só para todos os pedidos. Só entra quem tem conta (acesso ao sistema)
    /// e vínculo em andamento, de qualquer papel; a mesma conta aparece uma vez por alcance, mesmo morando em várias unidades dele.
    /// </summary>
    /// <returns>Um conjunto de contas para cada alcance pedido, na mesma ordem.</returns>
    Task<IReadOnlyList<IReadOnlySet<Guid>>> GetRecipientsAsync(IReadOnlyList<AudienceTarget> targets, DateOnly today, CancellationToken cancellationToken);
}
