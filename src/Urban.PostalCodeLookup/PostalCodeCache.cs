using System.Collections.Concurrent;
using Urban.SharedKernel.Abstractions;

namespace Urban.PostalCodeLookup;

/// <summary>
/// Guarda as respostas em memória do processo: CEP encontrado por 7 dias (o endereço quase nunca muda), "não existe" por 1 hora
/// (para um CEP recém-criado ou uma falha do provedor não ficarem errados por muito tempo) e "indisponível" nunca. O tamanho é
/// limitado: cheio, descarta as entradas vencidas e, se ainda faltar espaço, deixa de guardar CEPs novos.
/// </summary>
public sealed class PostalCodeCache(IClock clock, int maxEntries = PostalCodeCache.DefaultMaxEntries)
{
    public const int DefaultMaxEntries = 10_000;

    private static readonly TimeSpan FoundLifetime = TimeSpan.FromDays(7);

    private static readonly TimeSpan NotFoundLifetime = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    private readonly Lock _writeLock = new();

    public bool TryGet(string postalCode, out PostalCodeLookupResult result)
    {
        if (_entries.TryGetValue(postalCode, out Entry? entry))
        {
            if (entry.ExpiresAt > clock.UtcNow)
            {
                result = entry.Result;
                return true;
            }

            _entries.TryRemove(new KeyValuePair<string, Entry>(postalCode, entry));
        }

        result = PostalCodeLookupResult.Unavailable;
        return false;
    }

    public void Store(string postalCode, PostalCodeLookupResult result)
    {
        TimeSpan? lifetime = result.Status switch
        {
            PostalCodeLookupStatus.Found => FoundLifetime,
            PostalCodeLookupStatus.NotFound => NotFoundLifetime,
            _ => null,
        };
        if (lifetime is null)
        {
            return;
        }

        DateTimeOffset now = clock.UtcNow;
        lock (_writeLock)
        {
            if (_entries.Count >= maxEntries && !_entries.ContainsKey(postalCode))
            {
                RemoveExpired(now);
                if (_entries.Count >= maxEntries)
                {
                    return;
                }
            }

            _entries[postalCode] = new Entry(result, now + lifetime.Value);
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (KeyValuePair<string, Entry> pair in _entries.Where(p => p.Value.ExpiresAt <= now))
        {
            _entries.TryRemove(pair);
        }
    }

    private sealed record Entry(PostalCodeLookupResult Result, DateTimeOffset ExpiresAt);
}
