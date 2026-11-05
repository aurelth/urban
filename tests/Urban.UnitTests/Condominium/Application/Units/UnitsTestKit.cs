using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Units;

/// <summary>
/// Tudo o que um handler de unidades precisa, já ligado: o condomínio da requisição, o relógio, a unidade atômica e a porta
/// de persistência, que por padrão não acha conflito, tem soma zero e grava com sucesso. Cada teste muda só o que quer provar.
/// </summary>
internal sealed class UnitsTestKit
{
    public static readonly DateTimeOffset Now = new(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);

    public Guid CondominiumId { get; } = Guid.CreateVersion7();

    public Guid ActorId { get; } = Guid.CreateVersion7();

    public Mock<IUnits> Units { get; } = new();

    public Mock<IClock> Clock { get; } = new();

    public RecordingAtomicWork Atomic { get; } = new();

    public ITenantContext Tenant { get; private set; }

    public List<ChangeLogEntry> Logged { get; } = [];

    public List<CondominiumUnit> Added { get; } = [];

    public List<IReadOnlyList<CondominiumUnit>> AddedRanges { get; } = [];

    public List<UnitRecord> Saved { get; } = [];

    public UnitsTestKit()
    {
        Tenant = new FixedTenant(CondominiumId);
        Clock.SetupGet(c => c.UtcNow).Returns(Now);

        Units.Setup(u => u.IsIdentityTakenAsync(It.IsAny<UnitIdentity>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Units.Setup(u => u.TakenAmongAsync(It.IsAny<IReadOnlyCollection<UnitIdentity>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<UnitIdentity>());
        Units.Setup(u => u.ActiveFractionTotalAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(0m);
        Units.Setup(u => u.AddAsync(It.IsAny<CondominiumUnit>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<CondominiumUnit, ChangeLogEntry, CancellationToken>((unit, entry, _) =>
            {
                Added.Add(unit);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success("7"));
        Units.Setup(u => u.AddRangeAsync(It.IsAny<IReadOnlyList<CondominiumUnit>>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<CondominiumUnit>, ChangeLogEntry, CancellationToken>((units, entry, _) =>
            {
                AddedRanges.Add(units);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success());
        Units.Setup(u => u.SaveAsync(It.IsAny<UnitRecord>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<UnitRecord, ChangeLogEntry, CancellationToken>((record, entry, _) =>
            {
                Saved.Add(record);
                Logged.Add(entry);
            })
            .ReturnsAsync(Result.Success("8"));
    }

    /// <summary>A requisição de quem ainda não tem condomínio (o síndico antes do cadastro, ou outro papel).</summary>
    public UnitsTestKit WithoutATenant()
    {
        Tenant = new FixedTenant(null);

        return this;
    }

    /// <summary>Uma unidade que "já está no banco": a porta a devolve com a versão dada.</summary>
    public UnitRecord Existing(CondominiumUnit unit, string version = "5")
    {
        UnitRecord record = new(unit, version);
        Units.Setup(u => u.FindAsync(unit.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        return record;
    }

    public CondominiumUnit NewUnit(string number = "101", string? block = null, decimal? idealFraction = null, decimal? area = null) =>
        UnitBuilders.Unit(CondominiumId, block, number, idealFraction: idealFraction, area: area);
}
