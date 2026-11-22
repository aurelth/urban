using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Authentication;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.Api.Http;

/// <summary>
/// A porta de entrada das rotas de quem mora no condomínio: o morador e o síndico (que também pode morar nele). A conta e o papel vêm
/// do token assinado. Quem não tem sessão recebe 401; o super admin, que não pertence a condomínio nenhum, recebe 403. O que cada um
/// vê é decidido pelo vínculo dele com as unidades, nunca pelo papel nem por dado da requisição.
/// </summary>
public static class MemberAccess
{
    public static bool TryRead(ClaimsPrincipal user, out Guid userId, [NotNullWhen(false)] out IResult? refusal)
    {
        userId = Guid.Empty;
        if (!AccessTokenSession.TryRead(user, out AccessTokenSession? session))
        {
            refusal = ErrorHttp.ToProblem(SessionErrors.Unauthenticated);
            return false;
        }

        string? role = user.FindFirstValue(AccessTokenClaims.Role);

        if (role != nameof(UserRole.Resident) && role != nameof(UserRole.Manager))
        {
            refusal = ErrorHttp.ToProblem(UserErrors.NotCondominiumMember);
            return false;
        }

        userId = session.UserId;
        refusal = null;
        return true;
    }
}
