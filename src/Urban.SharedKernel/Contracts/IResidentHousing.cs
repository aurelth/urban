namespace Urban.SharedKernel.Contracts;

/// <param name="UnitId">A unidade.</param>
/// <param name="Block">O bloco, se o condomínio tem blocos.</param>
/// <param name="Number">O número da unidade.</param>
public sealed record ResidentUnit(Guid UnitId, string? Block, string Number);

/// <param name="Units">As unidades em que a pessoa mora hoje como proprietária ou inquilina, em ordem de bloco e número.</param>
/// <param name="LivesAsDependent">Se a pessoa também mora hoje como dependente de alguém (sem unidade própria para cobrar).</param>
public sealed record ResidentDwelling(IReadOnlyList<ResidentUnit> Units, bool LivesAsDependent);

/// <summary>
/// Contrato do módulo Condominium para o Financeiro: em que unidades uma conta mora. O Financeiro decide o que cada morador vê
/// da sua cobrança sem conhecer pessoas, vínculos nem convites.
/// </summary>
public interface IResidentHousing
{
    /// <summary>
    /// As unidades da conta na data: só vale vínculo de proprietário ou inquilino que já começou e ainda não terminou, no condomínio
    /// da requisição. Uma consulta só, nunca uma por unidade.
    /// </summary>
    /// <param name="userId">A conta que entrou (o token), nunca um dado de entrada da requisição.</param>
    /// <param name="today">A data de referência (Brasília).</param>
    /// <returns>Sem condomínio na requisição, ou sem pessoa ligada à conta, não há unidade nem dependência.</returns>
    Task<ResidentDwelling> GetAsync(Guid userId, DateOnly today, CancellationToken cancellationToken);
}
