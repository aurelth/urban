using Urban.Identity.Domain.Tokens;

namespace Urban.Identity.Application.Abstractions;

/// <summary>Porta de persistência dos tokens de renovação. Cada operação grava por conta própria (uma inserção ou um update por vez).</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);
}
