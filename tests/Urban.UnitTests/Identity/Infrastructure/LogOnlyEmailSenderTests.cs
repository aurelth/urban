using FluentAssertions;
using Microsoft.Extensions.Logging;
using Urban.Identity.Infrastructure.Messaging;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Identity.Infrastructure;

public class LogOnlyEmailSenderTests
{
    [Fact]
    public async Task SendAsync_LogsOnlyTheDomainAndTheSubject_NeverTheAddressOrTheBody()
    {
        ListLogger<LogOnlyEmailSender> logger = new();
        LogOnlyEmailSender sender = new(logger);

        await sender.SendAsync(new EmailMessage("sindica@example.com", "Assunto", "corpo-com-link-secreto"), default);

        LogEntry entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("example.com").And.Contain("Assunto");
        entry.Message.Should().NotContain("sindica").And.NotContain("corpo-com-link-secreto");
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}