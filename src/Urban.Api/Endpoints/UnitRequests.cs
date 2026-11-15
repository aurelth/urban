namespace Urban.Api.Endpoints;

/// <summary>
/// Os dados de uma unidade, para cadastrar (POST) e editar (PUT). <c>Type</c> é "apartment" ou "house"; qualquer outro valor
/// responde 400 com o código do campo. A fração vai de 0 a 1 (1,25% é 0,0125) e a área em m².
/// </summary>
/// <param name="Version">Só na edição: a versão lida em <c>GET /api/units/{id}</c>.</param>
/// <param name="ParkingSpots">Até 5 vagas de garagem, cada uma com até 20 caracteres e sem repetir, nem em outra unidade do condomínio. Ausente mantém as vagas atuais na edição; uma lista vazia as remove.</param>
public sealed record UnitRequest(string? Version, string? Block, string? Number, string? Type, decimal? IdealFraction, decimal? Area, IReadOnlyList<string?>? ParkingSpots = null);

/// <summary>Uma unidade dentro do cadastro em lote.</summary>
public sealed record UnitItemRequest(string? Block, string? Number, string? Type, decimal? IdealFraction, decimal? Area);

/// <param name="Units">De 1 a 500 unidades. Vazio ou ausente responde 400.</param>
public sealed record UnitBatchRequest(IReadOnlyList<UnitItemRequest?>? Units);

/// <summary>Desativar e reativar só precisam da versão que a tela leu.</summary>
public sealed record UnitVersionRequest(string? Version);

/// <param name="Method">"idealFraction", "equalShare" ou "area".</param>
/// <param name="Version">A versão lida em <c>GET /api/units/summary</c> (<c>apportionmentVersion</c>).</param>
public sealed record ApportionmentMethodRequest(string? Version, string? Method);
