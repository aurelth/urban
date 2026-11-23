namespace Urban.Api.Endpoints;

/// <param name="Recipients">Quantas contas de quem mora hoje no alcance do comunicado (sem o síndico que o escreveu).</param>
/// <param name="Readers">Quantas delas já marcaram como lido.</param>
/// <param name="NotifiedByEmail">Se o síndico pediu o e-mail ao publicar.</param>
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
    bool NotifiedByEmail,
    string Version);

public sealed record AnnouncementPageResponse(IReadOnlyList<AnnouncementResponse> Items, int Total, int Page, int PageSize);

/// <param name="Block">O bloco de um comunicado de alcance de bloco; nos outros alcances, nulo.</param>
/// <param name="Unit">A unidade de um comunicado de alcance de unidade; nos outros alcances, nulo.</param>
public sealed record MyAnnouncementResponse(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string Scope,
    string? Block,
    MyAnnouncementUnitResponse? Unit,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool Read,
    DateTimeOffset? ReadAt);

public sealed record MyAnnouncementUnitResponse(Guid Id, string? Block, string Number);

/// <param name="Unread">Quantos do mural a conta ainda não leu, no mural inteiro e não só nesta página.</param>
/// <param name="UnreadByCategory">O mesmo número separado por categoria; a soma é <paramref name="Unread"/>.</param>
public sealed record MyAnnouncementPageResponse(
    IReadOnlyList<MyAnnouncementResponse> Items, int Total, int Unread, UnreadByCategoryResponse UnreadByCategory, int Page, int PageSize);

public sealed record UnreadByCategoryResponse(int General, int Maintenance, int Important);

/// <param name="Marked">Quantos comunicados foram marcados agora; os que a conta já tinha lido não entram.</param>
public sealed record MarkedReadResponse(int Marked);
