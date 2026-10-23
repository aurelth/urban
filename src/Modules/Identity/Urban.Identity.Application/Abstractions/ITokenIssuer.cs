using Urban.Identity.Domain.Users;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta para emitir o token de acesso (JWT na Infrastructure).</summary>
public interface ITokenIssuer
{
    AccessToken IssueAccessToken(User user, DateTimeOffset now);
}
