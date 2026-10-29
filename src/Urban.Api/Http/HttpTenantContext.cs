using System.Security.Claims;
using Urban.Identity.Application.Abstractions;
using Urban.SharedKernel.Abstractions;

namespace Urban.Api.Http;

/// <summary>O condomínio da requisição vem da claim <c>condominium_id</c> do token de acesso, que o Identity emite a partir da conta.</summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? CondominiumId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(AccessTokenClaims.CondominiumId), out Guid id) && id != Guid.Empty
            ? id
            : null;
}
