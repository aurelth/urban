using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.PostalCodeLookup;

/// <summary>Consulta de CEP com resposta combinada de antemão, que anota quantas vezes e com que CEP foi chamada.</summary>
internal sealed class ScriptedLookup(params PostalCodeLookupResult[] results) : IPostalCodeLookup
{
    public static readonly PostalCodeAddress Paulista = new("01310100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP");

    public List<string> Requested { get; } = [];

    public Task<PostalCodeLookupResult> LookupAsync(string postalCode, CancellationToken cancellationToken)
    {
        Requested.Add(postalCode);
        PostalCodeLookupResult result = results[Math.Min(Requested.Count, results.Length) - 1];
        return Task.FromResult(result);
    }
}
