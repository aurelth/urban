using System.Security.Claims;
using Urban.Identity.Application.Abstractions;
using Urban.SharedKernel.Abstractions;

namespace Urban.Api.Http;

/// <summary>
/// O condomínio da requisição vem da claim <c>condominium_id</c> do token de acesso, que o Identity emite a partir da conta. Uma
/// tarefa em segundo plano não tem token: ela escolhe o condomínio com <see cref="Enter"/>, que vale só no escopo dela.
/// </summary>
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext, ITenantScope
{
    private Guid? _entered;

    public Guid? CondominiumId =>
        _entered ?? (Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(AccessTokenClaims.CondominiumId), out Guid id) && id != Guid.Empty
            ? id
            : null);

    public void Enter(Guid condominiumId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(condominiumId, Guid.Empty);

        _entered = condominiumId;
    }
}
