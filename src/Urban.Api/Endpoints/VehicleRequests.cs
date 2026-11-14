namespace Urban.Api.Endpoints;

/// <summary>
/// Os dados de um veículo, para cadastrar (POST) e editar (PUT). A placa vai em qualquer caixa e com ou sem hífen
/// (ABC-1234, abc1d23); quem diz se vale é o Domain, com o código do campo.
/// </summary>
/// <param name="Version">Só na edição: a versão lida em <c>GET /api/vehicles/{id}</c>.</param>
/// <param name="UnitId">A unidade do veículo: precisa ser do condomínio e estar ativa.</param>
public sealed record VehicleRequest(string? Version, Guid? UnitId, string? Plate, string? Model, string? Color);
