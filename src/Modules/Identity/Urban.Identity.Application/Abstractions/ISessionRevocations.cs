namespace Urban.Identity.Application.Abstractions;

/// <summary>
/// Porta que invalida sessões já emitidas (Redis na Infrastructure). Os tokens de acesso são curtos e não ficam no banco,
/// então a revogação vale por um instante: todo token emitido antes dele deixa de ser aceito.
/// </summary>
public interface ISessionRevocations
{
    Task RevokeAllOfUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken);
}
