using Riok.Mapperly.Abstractions;
using Urban.Api.Endpoints;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;

namespace Urban.Api.Mapping;

[Mapper]
public static partial class UnitMapper
{
    private static readonly UnitItemRequest NoUnit = new(null, null, null, null, null);

    // ---- Do corpo da requisição para o comando ----

    public static CreateUnitCommand ToCreateCommand(UnitRequest request, Guid actorUserId) =>
        new(actorUserId, request.Block, request.Number, ParseType(request.Type), request.IdealFraction, request.Area);

    public static UpdateUnitCommand ToUpdateCommand(UnitRequest request, Guid unitId, Guid actorUserId) =>
        new(actorUserId, unitId, request.Version, request.Block, request.Number, ParseType(request.Type), request.IdealFraction, request.Area);

    public static CreateUnitsBatchCommand ToBatchCommand(UnitBatchRequest request, Guid actorUserId) =>
        new(actorUserId, [.. (request.Units ?? []).Select(item => ToInput(item ?? NoUnit))]);

    public static ChangeApportionmentMethodCommand ToCommand(ApportionmentMethodRequest request, Guid actorUserId) =>
        new(actorUserId, request.Version, ParseMethod(request.Method));

    /// <summary>Um item sem dados vira uma unidade vazia: quem diz qual campo falta é o Domain, com o código do campo.</summary>
    private static UnitInput ToInput(UnitItemRequest item) =>
        new(item.Block, item.Number, ParseType(item.Type), item.IdealFraction, item.Area);

    // ---- Do resultado para a resposta ----

    public static partial UnitResponse ToResponse(UnitView view);

    public static partial UnitListResponse ToResponse(UnitListing listing);

    [MapProperty(nameof(UnitsSummary.Method), nameof(UnitsSummaryResponse.ApportionmentMethod))]
    public static partial UnitsSummaryResponse ToResponse(UnitsSummary summary);

    public static partial ApportionmentMethodResponse ToResponse(ApportionmentView view);

    public static UnitBatchCreatedResponse ToCreatedResponse(UnitBatchResult result) => new(result.Created);

    public static IReadOnlyList<UnitBatchIssueResponse> ToIssues(UnitBatchResult result) =>
        [.. result.Issues.Select(issue => new UnitBatchIssueResponse(issue.Position, issue.Code))];

    // ---- Texto da API <-> enum (camelCase). Valor desconhecido vira 0, que o Domain recusa com o código do campo ----

    public static UnitType ParseType(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "apartment" => UnitType.Apartment,
        "house" => UnitType.House,
        _ => 0,
    };

    public static ApportionmentMethod ParseMethod(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "idealfraction" => ApportionmentMethod.IdealFraction,
        "equalshare" => ApportionmentMethod.EqualShare,
        "area" => ApportionmentMethod.Area,
        _ => 0,
    };

    /// <summary>Texto do filtro de tipo da lista. Nulo é "sem filtro"; um texto desconhecido vira 0 e a lista responde 400 com o código do tipo.</summary>
    public static UnitType? ParseTypeFilter(string? text) => string.IsNullOrWhiteSpace(text) ? null : ParseType(text);

    /// <returns>Falso quando o texto não é um estado conhecido; vazio é o padrão (ativas).</returns>
    public static bool TryParseStatus(string? text, out UnitStatusFilter status)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case null or "":
            case "active":
                status = UnitStatusFilter.Active;
                return true;
            case "deactivated":
                status = UnitStatusFilter.Deactivated;
                return true;
            case "all":
                status = UnitStatusFilter.All;
                return true;
            default:
                status = default;
                return false;
        }
    }

    private static string TypeName(UnitType type) => type switch
    {
        UnitType.Apartment => "apartment",
        UnitType.House => "house",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de unidade desconhecido."),
    };

    private static string MethodName(ApportionmentMethod method) => method switch
    {
        ApportionmentMethod.IdealFraction => "idealFraction",
        ApportionmentMethod.EqualShare => "equalShare",
        ApportionmentMethod.Area => "area",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Método de rateio desconhecido."),
    };
}
