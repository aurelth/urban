namespace Urban.Identity.Application.Authentication.Me;

/// <param name="Role">O mesmo texto da claim <c>role</c> do token de acesso.</param>
/// <param name="CondominiumId">Nulo para o SuperAdmin e para o síndico que ainda não cadastrou o condomínio.</param>
public sealed record CurrentUser(Guid Id, string Email, string Role, Guid? CondominiumId);