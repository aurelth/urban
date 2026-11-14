using Urban.Condominium.Domain.Vehicles;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>Um veículo como a leitura do banco o entrega, com o bloco e o número da unidade (para a tela mostrar sem consultar de novo) e a versão da linha (no PostgreSQL, o <c>xmin</c>).</summary>
public sealed record VehicleRecord(Vehicle Vehicle, string? UnitBlock, string UnitNumber, string Version);
