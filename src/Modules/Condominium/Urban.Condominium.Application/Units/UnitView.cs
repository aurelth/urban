using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Units;

namespace Urban.Condominium.Application.Units;

/// <summary>Uma unidade como a tela a vê. A fração vai de 0 a 1; quem mostra converte para percentual.</summary>
/// <param name="Version">Devolvida na edição, na desativação e na reativação para detectar que outra pessoa mudou a unidade no meio do caminho.</param>
public sealed record UnitView(
    Guid Id,
    string? Block,
    string Number,
    UnitType Type,
    decimal? IdealFraction,
    decimal? Area,
    bool IsActive,
    DateTimeOffset? DeactivatedAt,
    string Version)
{
    internal static UnitView From(CondominiumUnit unit, string version) =>
        new(unit.Id, unit.Block, unit.Number, unit.Type, unit.IdealFraction, unit.Area, unit.IsActive, unit.DeactivatedAt, version);

    internal static UnitView From(UnitRecord record) => From(record.Unit, record.Version);
}
