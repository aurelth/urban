namespace Urban.Condominium.Domain.Condominiums;

/// <summary>
/// Como o condomínio divide as despesas entre as unidades. Aqui só se guarda a escolha; o cálculo mora no módulo de
/// cobranças. Os valores começam em 1: o 0 nunca é um método válido.
/// </summary>
public enum ApportionmentMethod
{
    /// <summary>Cada unidade paga a sua fração ideal (o padrão).</summary>
    IdealFraction = 1,

    /// <summary>Todas as unidades pagam o mesmo valor.</summary>
    EqualShare = 2,

    /// <summary>Cada unidade paga em proporção à sua área.</summary>
    Area = 3,
}
