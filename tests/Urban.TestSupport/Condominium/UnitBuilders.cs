using Urban.Condominium.Domain.Units;

namespace Urban.TestSupport.Condominium;

/// <summary>Monta unidades válidas pela API pública do Domain, para os testes mudarem só o campo que querem provar.</summary>
public static class UnitBuilders
{
    public static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    public static CondominiumUnit Unit(
        Guid? condominiumId = null,
        string? block = null,
        string number = "101",
        UnitType type = UnitType.Apartment,
        decimal? idealFraction = null,
        decimal? area = null) =>
        CondominiumUnit.Create(condominiumId ?? Guid.CreateVersion7(), block, number, type, idealFraction, area, Now).Value;

    /// <summary>Uma unidade já desativada.</summary>
    public static CondominiumUnit Deactivated(Guid? condominiumId = null, string number = "101", decimal? idealFraction = null)
    {
        CondominiumUnit unit = Unit(condominiumId, number: number, idealFraction: idealFraction);
        unit.Deactivate(Now.AddDays(1));

        return unit;
    }
}
