namespace Urban.Identity.Application.Abstractions;

/// <summary>
/// Porta de consulta das revogações, chamada a cada requisição autenticada. Fica separada de
/// <see cref="ISessionRevocations"/> (quem revoga) porque quem pergunta e quem revoga são partes diferentes do sistema.
/// </summary>
public interface ISessionRevocationChecker
{
    /// <summary>
    /// O token foi revogado, ou foi emitido até o instante da última revogação geral do usuário?
    /// <paramref name="issuedAt"/> é o instante de emissão do token (claim <c>iat</c>).
    /// </summary>
    Task<bool> IsRevokedAsync(string jti, Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken);
}