using Urban.Identity.Application.Abstractions;

namespace Urban.IntegrationTests.Identity.Persistence;

/// <summary>Hasher de teste: delega ao verdadeiro, conta as chamadas e deixa o teste intervir no meio da verificação.</summary>
internal sealed class SpyPasswordHasher(IPasswordHasher inner) : IPasswordHasher
{
    public int VerifyCalls { get; private set; }

    public int DummyCalls { get; private set; }

    /// <summary>Executada uma única vez, no começo da primeira verificação: simula outra requisição chegando ao mesmo tempo.</summary>
    public Func<Task>? ConcurrentWork { get; set; }

    public string Hash(string password) => inner.Hash(password);

    public bool Verify(string password, string hash)
    {
        VerifyCalls++;
        Func<Task>? work = ConcurrentWork;
        ConcurrentWork = null;
        work?.Invoke().GetAwaiter().GetResult();
        return inner.Verify(password, hash);
    }

    public void VerifyDummy(string password)
    {
        DummyCalls++;
        inner.VerifyDummy(password);
    }
}