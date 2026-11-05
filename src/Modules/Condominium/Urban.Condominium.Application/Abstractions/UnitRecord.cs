using Urban.Condominium.Domain.Units;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>Uma unidade como a leitura do banco a entrega, com a versão da linha (no PostgreSQL, o <c>xmin</c>). Quem edita devolve a versão que leu.</summary>
public sealed record UnitRecord(CondominiumUnit Unit, string Version);
