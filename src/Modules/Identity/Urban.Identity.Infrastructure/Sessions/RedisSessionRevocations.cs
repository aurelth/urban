using StackExchange.Redis;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Infrastructure.Tokens;
using Urban.SharedKernel.Abstractions;

namespace Urban.Identity.Infrastructure.Sessions;

/// <summary>
/// Revogações de sessão no Redis. Duas chaves, ambas com validade (TTL) igual ao tempo que o token ainda viveria, então
/// nada se acumula: <c>identity:revoked-token:{jti}</c> recusa um token de acesso específico (logout) e
/// <c>identity:revoked-user:{id}</c> guarda o instante, em segundos, até o qual todo token do usuário é recusado
/// (suspensão, reuso de refresh token). O cliente do Redis não aceita <see cref="CancellationToken"/>; os parâmetros existem
/// só para cumprir as portas.
/// </summary>
public sealed class RedisSessionRevocations(IConnectionMultiplexer redis, IClock clock, JwtOptions jwt)
    : ISessionRevocations, ISessionRevocationChecker
{
    /// <summary>
    /// Grava o instante só se for maior que o já guardado: uma revogação mais antiga que chega atrasada nunca desfaz uma
    /// mais nova. Roda dentro do Redis, então a leitura e a gravação são uma operação única.
    /// </summary>
    private const string KeepNewestScript = """
        local current = redis.call('GET', KEYS[1])
        if not current or tonumber(current) < tonumber(ARGV[1]) then
            redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
        end
        """;

    public static string TokenKey(string jti) => $"identity:revoked-token:{jti}";

    public static string UserKey(Guid userId) => $"identity:revoked-user:{userId}";

    public async Task RevokeAccessTokenAsync(string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        TimeSpan remaining = expiresAt - clock.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await redis.GetDatabase().StringSetAsync(TokenKey(jti), "1", remaining);
        }
        catch (RedisException ex)
        {
            throw new SessionStoreUnavailableException(ex);
        }
    }

    public async Task RevokeAllOfUserAsync(Guid userId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        // Depois de at + validade do token de acesso, nenhum token emitido até at continua vivo: a chave já pode sumir.
        TimeSpan remaining = at + jwt.AccessTokenLifetime - clock.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        RedisKey[] keys = [UserKey(userId)];
        RedisValue[] values = [at.ToUnixTimeSeconds(), (long)remaining.TotalMilliseconds];
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(KeepNewestScript, keys, values);
        }
        catch (RedisException ex)
        {
            throw new SessionStoreUnavailableException(ex);
        }
    }

    public async Task<bool> IsRevokedAsync(string jti, Guid userId, DateTimeOffset issuedAt, CancellationToken cancellationToken)
    {
        // Um único MGET traz as duas chaves: uma ida ao Redis por requisição autenticada.
        RedisValue[] found;
        try
        {
            found = await redis.GetDatabase().StringGetAsync([TokenKey(jti), UserKey(userId)]);
        }
        catch (RedisException ex)
        {
            throw new SessionStoreUnavailableException(ex);
        }

        return found[0].HasValue
            || (found[1].TryParse(out long revokedUntil) && issuedAt.ToUnixTimeSeconds() <= revokedUntil);
    }
}