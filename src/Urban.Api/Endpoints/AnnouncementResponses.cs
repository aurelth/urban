namespace Urban.Api.Endpoints;

/// <param name="Recipients">Quantas contas de quem mora hoje no alcance do comunicado (sem o síndico que o escreveu).</param>
/// <param name="Readers">Quantas delas já marcaram como lido.</param>
public sealed record AnnouncementResponse(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string Scope,
    string? Block,
    Guid? UnitId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Recipients,
    int Readers,
    string Version);

public sealed record AnnouncementPageResponse(IReadOnlyList<AnnouncementResponse> Items, int Total, int Page, int PageSize);

public sealed record MyAnnouncementResponse(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string Scope,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool Read,
    DateTimeOffset? ReadAt);

/// <param name="Unread">Quantos do mural a conta ainda não leu, no mural inteiro e não só nesta página.</param>
public sealed record MyAnnouncementPageResponse(IReadOnlyList<MyAnnouncementResponse> Items, int Total, int Unread, int Page, int PageSize);
