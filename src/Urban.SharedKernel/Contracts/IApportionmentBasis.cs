namespace Urban.SharedKernel.Contracts;

/// <summary>Em que o rateio do condomínio se baseia: o peso de cada unidade vem da fração ideal, da área ou é igual para todas.</summary>
public enum ApportionmentBasisKind
{
    IdealFraction = 1,
    EqualShare = 2,
    Area = 3,
}

/// <param name="Weight">Quanto a unidade pesa no rateio: a fração ideal (0 a 1), a área em m² ou 1 quando todas pagam igual. Só vale a proporção entre as unidades.</param>
public sealed record ApportionmentUnit(Guid UnitId, string? Block, string Number, decimal Weight);

/// <param name="Units">Só as unidades ativas, na ordem natural (bloco e depois número).</param>
public sealed record ApportionmentBasis(ApportionmentBasisKind Kind, IReadOnlyList<ApportionmentUnit> Units);

/// <summary>
/// Contrato do módulo Condominium para os outros módulos: quem rateia (o Financeiro) pede as unidades com o peso do método
/// que o síndico escolheu e não conhece as camadas internas do Condominium.
/// </summary>
public interface IApportionmentBasis
{
    /// <returns>As unidades e os pesos do condomínio da requisição, ou a falha: condomínio inexistente ou dados das unidades que ainda não bastam para o método escolhido.</returns>
    Task<Result<ApportionmentBasis>> GetAsync(CancellationToken cancellationToken);
}
