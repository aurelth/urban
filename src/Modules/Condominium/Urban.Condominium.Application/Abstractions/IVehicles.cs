using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>
/// Porta de persistência dos veículos, sempre dentro do condomínio da requisição (tenant). A consulta da placa serve para
/// dar uma resposta clara; quem garante que a placa é única de verdade é o índice do banco, que as gravações traduzem em conflito.
/// </summary>
public interface IVehicles
{
    Task<VehicleRecord?> FindAsync(Guid vehicleId, CancellationToken cancellationToken);

    /// <summary>Uma página na ordem da unidade (bloco e número naturais, sem bloco primeiro) e depois da placa.</summary>
    Task<PagedVehicles> ListAsync(VehicleFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Se a placa (já normalizada) já está cadastrada, sem contar <paramref name="exceptVehicleId"/> (o veículo que está sendo editado).</summary>
    Task<bool> IsPlateTakenAsync(string plate, Guid? exceptVehicleId, CancellationToken cancellationToken);

    /// <returns>A versão do novo veículo, ou a falha: placa repetida.</returns>
    Task<Result<string>> AddAsync(Vehicle vehicle, ChangeLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Grava as mudanças feitas no veículo lido por <see cref="FindAsync"/> junto com o registro, numa gravação só.</summary>
    /// <returns>A nova versão, ou a falha: conflito de versão ou placa repetida.</returns>
    Task<Result<string>> SaveAsync(VehicleRecord record, ChangeLogEntry entry, CancellationToken cancellationToken);

    /// <summary>Apaga o veículo lido por <see cref="FindAsync"/> junto com o registro, numa gravação só.</summary>
    Task<Result> DeleteAsync(VehicleRecord record, ChangeLogEntry entry, CancellationToken cancellationToken);
}
