using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>Um morador como a leitura do banco o entrega: a pessoa, os vínculos com as unidades e a versão da linha da pessoa (no PostgreSQL, o <c>xmin</c>).</summary>
public sealed record ResidentRecord(Person Person, IReadOnlyList<ResidentOccupancy> Occupancies, string Version);

/// <summary>Um vínculo junto com o bloco e o número da unidade, para a tela mostrar sem consultar de novo.</summary>
public sealed record ResidentOccupancy(Occupancy Occupancy, string? UnitBlock, string UnitNumber);
