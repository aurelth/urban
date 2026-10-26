namespace Urban.Identity.Application.Authentication.Me;

/// <param name="Role">O mesmo texto da claim <c>role</c> do token de acesso.</param>
/// <param name="CondominiumId">Nulo para o SuperAdmin, que não pertence a um condomínio.</param>
public sealed record CurrentUser(Guid Id, string Email, string Role, Guid? CondominiumId);