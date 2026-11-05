using Urban.Condominium.Domain.Condominiums;

namespace Urban.Condominium.Application.Units;

/// <summary>O retrato das unidades: quantas são, quanto falta preencher e se cada método de rateio já pode ser usado.</summary>
/// <param name="IdealFractionTotal">Soma das frações das unidades ativas, de 0 a 1.</param>
/// <param name="WithoutIdealFraction">Unidades ativas sem fração ideal.</param>
/// <param name="WithoutArea">Unidades ativas sem área.</param>
/// <param name="ApportionmentVersion">Para trocar o método sem pisar em outra edição.</param>
public sealed record UnitsSummary(
    int Active,
    int Deactivated,
    int Houses,
    int Apartments,
    decimal IdealFractionTotal,
    int WithoutIdealFraction,
    int WithoutArea,
    ApportionmentMethod Method,
    string ApportionmentVersion,
    IReadOnlyList<MethodReadiness> Readiness);

public sealed record MethodReadiness(ApportionmentMethod Method, bool Ready);
