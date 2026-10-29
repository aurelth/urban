using System.Collections.Concurrent;
using Urban.SharedKernel.Abstractions;

namespace Urban.IntegrationTests.Api;

/// <summary>Responde no lugar da BrasilAPI e do ViaCEP: os testes de integração nunca saem para a rede.</summary>
public sealed class FakePostalCodeLookup : IPostalCodeLookup
{
    private readonly ConcurrentQueue<string> _requested = new();

    public PostalCodeLookupResult Result { get; set; } = PostalCodeLookupResult.Unavailable;

    public string[] Requested => [.. _requested];

    public Task<PostalCodeLookupResult> LookupAsync(string postalCode, CancellationToken cancellationToken)
    {
        _requested.Enqueue(postalCode);
        return Task.FromResult(Result);
    }

    public void Reset()
    {
        Result = PostalCodeLookupResult.Unavailable;
        _requested.Clear();
    }
}
