using Urban.SharedKernel.Abstractions;

namespace Urban.SharedKernel.Time;

/// <summary>Relógio real, usado em produção. Os testes trocam por um relógio fixo.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
