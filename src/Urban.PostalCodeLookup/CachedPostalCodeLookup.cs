using Urban.SharedKernel.Abstractions;

namespace Urban.PostalCodeLookup;

/// <summary>
/// Consulta o cache antes dos provedores. O cache é um objeto à parte, de vida longa, para a consulta em si poder ser criada a
/// cada uso (o HttpClient tipado não deve ser guardado por muito tempo) sem perder o que já foi respondido.
/// </summary>
public sealed class CachedPostalCodeLookup(IPostalCodeLookup inner, PostalCodeCache cache) : IPostalCodeLookup
{
    public async Task<PostalCodeLookupResult> LookupAsync(string postalCode, CancellationToken cancellationToken)
    {
        if (cache.TryGet(postalCode, out PostalCodeLookupResult cached))
        {
            return cached;
        }

        PostalCodeLookupResult result = await inner.LookupAsync(postalCode, cancellationToken);
        cache.Store(postalCode, result);

        return result;
    }
}
