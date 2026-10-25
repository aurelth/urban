using Microsoft.Extensions.Logging;

namespace Urban.UnitTests.Support;

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

/// <summary>Logger que guarda o que recebeu, para testar o que é (e o que não é) registrado.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
}