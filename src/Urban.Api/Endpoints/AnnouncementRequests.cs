namespace Urban.Api.Endpoints;

/// <param name="Version">A versão lida na listagem; só a edição a exige.</param>
/// <param name="Category"><c>general</c>, <c>maintenance</c> ou <c>important</c>.</param>
/// <param name="Scope"><c>condominium</c>, <c>block</c> ou <c>unit</c>.</param>
/// <param name="Block">O bloco, só no alcance de bloco.</param>
/// <param name="UnitId">A unidade, só no alcance de unidade.</param>
/// <param name="NotifyByEmail">Só na criação: manda o comunicado por e-mail a quem mora no alcance dele. Na edição é ignorado.</param>
public sealed record AnnouncementRequest(string? Version, string? Title, string? Body, string? Category, string? Scope, string? Block, Guid? UnitId, bool? NotifyByEmail = null);
