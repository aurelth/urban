namespace Urban.Identity.Application.Abstractions;

/// <summary>
/// Porta que invalida sessões já emitidas (Redis na Infrastructure). Os tokens de acesso são curtos e não ficam no banco,
/// então a revogação vale por um instante ou por um identificador (jti), e só precisa durar até o token expirar.
/// </summary>
public interface ISessionRevocations
{
    /// <summary>Todo token de acesso do usuário emitido antes de <paramref name="at"/> deixa de ser aceito.</summary>
    Task RevokeAllOfUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Recusa este token de acesso até <paramref name="expiresAt"/>. Token que já expirou pode ser ignorado.</summary>
    Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}
