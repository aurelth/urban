namespace Urban.Condominium.Domain.Units;

/// <summary>O que impede o método de rateio de funcionar.</summary>
public enum ApportionmentShortfallReason
{
    /// <summary>Não há nenhuma unidade ativa para ratear.</summary>
    NoActiveUnits = 1,

    /// <summary>Há unidades ativas sem fração ideal.</summary>
    FractionsMissing = 2,

    /// <summary>Todas têm fração, mas a soma não chega a 100%.</summary>
    FractionsTotal = 3,

    /// <summary>Há unidades ativas sem área.</summary>
    AreasMissing = 4,
}

/// <summary>Como uma unidade é chamada nas mensagens: bloco (opcional) e número.</summary>
public sealed record UnitReference(string? Block, string Number);

/// <summary>
/// O que falta para o método de rateio funcionar, com os dados que explicam: as unidades sem o valor (em <see cref="Units"/>, na
/// ordem recebida) ou a soma atual das frações (em <see cref="FractionTotal"/>, em decimal: 0,05 é 5%).
/// </summary>
public sealed record ApportionmentShortfall(ApportionmentShortfallReason Reason, IReadOnlyList<UnitReference> Units, decimal? FractionTotal = null)
{
    public static ApportionmentShortfall NoActiveUnits() => new(ApportionmentShortfallReason.NoActiveUnits, []);

    public static ApportionmentShortfall FractionsMissing(IReadOnlyList<UnitReference> units) => new(ApportionmentShortfallReason.FractionsMissing, units);

    public static ApportionmentShortfall FractionsTotal(decimal total) => new(ApportionmentShortfallReason.FractionsTotal, [], total);

    public static ApportionmentShortfall AreasMissing(IReadOnlyList<UnitReference> units) => new(ApportionmentShortfallReason.AreasMissing, units);
}
