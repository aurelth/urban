using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta de persistência dos tokens de renovação. Cada operação grava por conta própria e de forma atômica.</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Grava as alterações de um token que foi carregado por <see cref="FindByHashAsync"/>.</summary>
    Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>Grava o token revogado e o seu substituto na mesma transação.</summary>
    Task ReplaceAsync(RefreshToken current, RefreshToken replacement, CancellationToken cancellationToken);

    /// <summary>Revoga, em uma única operação, todos os tokens ainda ativos do usuário.</summary>
    Task RevokeAllOfUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}
