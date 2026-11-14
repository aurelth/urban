namespace Urban.Api.Endpoints;

/// <summary>Um morador como a tela o vê. O CPF só sai mascarado. <c>AccessStatus</c> é "none" (sem convite), "invited" (convite enviado, senha ainda não escolhida: o e-mail ainda se corrige), "active" ou "inactive" (conta desativada ou suspensa); depois da senha o e-mail não muda mais. <c>Version</c> volta na edição.</summary>
public sealed record ResidentResponse(
    Guid Id,
    string FullName,
    string MaskedCpf,
    DateOnly BirthDate,
    string Phone,
    string? Email,
    string? Profession,
    EmergencyContactResponse? EmergencyContact,
    IReadOnlyList<ResidentOccupancyResponse> Occupancies,
    string AccessStatus,
    string Version);

public sealed record EmergencyContactResponse(string Name, string Relationship, string Phone);

/// <summary><c>Kind</c> é "owner" ou "tenant". <c>EndDate</c> é nula enquanto a pessoa mora na unidade.</summary>
public sealed record ResidentOccupancyResponse(
    Guid Id,
    Guid UnitId,
    string? UnitBlock,
    string UnitNumber,
    string Kind,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record ResidentListResponse(IReadOnlyList<ResidentResponse> Items, int Total, int Page, int PageSize);
