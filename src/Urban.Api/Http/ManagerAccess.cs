using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.Api.Http;

/// <summary>
/// A porta de entrada das rotas do síndico: a conta e o papel vêm do token assinado. Quem não tem sessão recebe 401; quem não
/// é síndico, 403. O condomínio não é lido aqui: cada comando o pega do contexto da requisição e responde 404 se não houver.
/// </summary>
public static class ManagerAccess
{
    public static bool TryRead(ClaimsPrincipal user, out Guid userId, [NotNullWhen(false)] out IResult? refusal)
    {
        userId = Guid.Empty;
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            refusal = ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
            return false;
        }

        if (user.FindFirstValue(AccessTokenClaims.Role) != nameof(UserRole.Manager))
        {
            refusal = ErrorHttp.ToProblem(UserErrors.NotManager);
            return false;
        }

        userId = session.UserId;
        refusal = null;
        return true;
    }
}
