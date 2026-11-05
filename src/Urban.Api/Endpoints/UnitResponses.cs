namespace Urban.Api.Endpoints;

/// <summary>Uma unidade como a tela a vê. <c>Type</c> é "apartment" ou "house"; a fração vai de 0 a 1.</summary>
public sealed record UnitResponse(
    Guid Id,
    string? Block,
    string Number,
    string Type,
    decimal? IdealFraction,
    decimal? Area,
    bool IsActive,
    DateTimeOffset? DeactivatedAt,
    string Version);

public sealed record UnitListResponse(IReadOnlyList<UnitResponse> Items, int Total, int Page, int PageSize);

public sealed record UnitsSummaryResponse(
    int Active,
    int Deactivated,
    int Houses,
    int Apartments,
    decimal IdealFractionTotal,
    int WithoutIdealFraction,
    int WithoutArea,
    string ApportionmentMethod,
    string ApportionmentVersion,
    IReadOnlyList<MethodReadinessResponse> Readiness);

public sealed record MethodReadinessResponse(string Method, bool Ready);

public sealed record ApportionmentMethodResponse(string Method, string Version);

public sealed record UnitBatchCreatedResponse(int Created);

/// <summary>Um problema do cadastro em lote: a posição da unidade (a partir de 0, na ordem enviada) e o código do erro.</summary>
public sealed record UnitBatchIssueResponse(int Position, string Code);
