using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Vehicles;

/// <summary>
/// Tudo o que um handler de veículos precisa, já ligado: o condomínio da requisição, o relógio e as portas de veículos e
/// unidades. Por padrão a placa está livre e a gravação dá certo; cada teste muda só o que quer provar.
/// </summary>
internal sealed class VehiclesTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 11, 14, 15, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    public Guid ActorId { get; } = Guid.CreateVersion7();

    public Mock<IVehicles> Vehicles { get; } = new();

    public Mock<IUnits> Units { get; } = new();

    public Mock<IClock> Clock { get; } = new();

    public ITenantContext Tenant { get; private set; }

    public List<ChangeLogEntry> Logged { get; } = [];

    public List<Vehicle> Added { get; } = [];

    public List<VehicleRecord> Saved { get; } = [];

    public List<VehicleRecord> Deleted { get; } = [];

    public VehiclesTestKit()
    {
        Tenant = new FixedTenant(CondominiumId);
        Clock.SetupGet(c => c.UtcNow).Returns(Now);

        Vehicles.Setup(v => v.IsPlateTakenAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Vehicles.Setup(v => v.AddAsync(It.IsAny<Vehicle>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<Vehicle, ChangeLogEntry, CancellationToken>((vehicle, entry, _) =>
            {
                Added.Add(vehicle);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success("7"));
        Vehicles.Setup(v => v.SaveAsync(It.IsAny<VehicleRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<VehicleRecord, ChangeLogEntry, CancellationToken>((record, entry, _) =>
            {
                Saved.Add(record);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success("8"));
        Vehicles.Setup(v => v.DeleteAsync(It.IsAny<VehicleRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<VehicleRecord, ChangeLogEntry, CancellationToken>((record, entry, _) =>
            {
                Deleted.Add(record);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success());
    }

    /// <summary>A requisição de quem não tem condomínio (o síndico antes do cadastro, ou outro papel).</summary>
    public VehiclesTestKit WithoutATenant()
    {
        Tenant = new FixedTenant(null);

        return this;
    }

    /// <summary>Uma unidade ativa que "já está no banco".</summary>
    public CondominiumUnit ActiveUnit(string number = "101", string? block = "Bloco A")
    {
        CondominiumUnit unit = UnitBuilders.Unit(CondominiumId, block, number);
        Units.Setup(u => u.FindAsync(unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new UnitRecord(unit, "1"));

        return unit;
    }

    public CondominiumUnit DeactivatedUnit(string number = "999")
    {
        CondominiumUnit unit = UnitBuilders.Deactivated(CondominiumId, number);
        Units.Setup(u => u.FindAsync(unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new UnitRecord(unit, "1"));

        return unit;
    }

    /// <summary>Um veículo que "já está no banco", numa unidade, com a versão dada.</summary>
    public VehicleRecord Existing(CondominiumUnit unit, string plate = "ABC1D23", string model = "Fiat Argo", string color = "Prata", string version = "5")
    {
        Vehicle vehicle = VehicleBuilders.Build(CondominiumId, unit.Id, plate, model, color);
        VehicleRecord record = new(vehicle, unit.Block, unit.Number, version);
        Vehicles.Setup(v => v.FindAsync(vehicle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        return record;
    }
}
