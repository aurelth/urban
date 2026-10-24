using Urban.SharedKernel.Abstractions;

namespace Urban.TestSupport.Time;

/// <summary>Relógio de teste: fica parado até alguém avançá-lo, para os testes controlarem a passagem do tempo.</summary>
public sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public void Advance(TimeSpan time) => UtcNow += time;
}