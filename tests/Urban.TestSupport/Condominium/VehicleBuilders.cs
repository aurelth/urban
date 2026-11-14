using Urban.Condominium.Domain.Vehicles;

namespace Urban.TestSupport.Condominium;

/// <summary>Monta veículos válidos pela API pública do Domain, para os testes mudarem só o campo que querem provar.</summary>
public static class VehicleBuilders
{
    public static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    public static Vehicle Build(
        Guid? condominiumId = null,
        Guid? unitId = null,
        string plate = "ABC1D23",
        string model = "Fiat Argo",
        string color = "Prata") =>
        Vehicle.Create(condominiumId ?? Guid.CreateVersion7(), unitId ?? Guid.CreateVersion7(), plate, model, color, Now).Value;
}
