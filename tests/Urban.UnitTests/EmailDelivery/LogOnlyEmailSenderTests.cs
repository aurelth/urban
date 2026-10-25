using FluentAssertions;
using Microsoft.Extensions.Logging;
using Urban.EmailDelivery;
using Urban.SharedKernel.Abstractions;
using Urban.UnitTests.Support;

namespace Urban.UnitTests.EmailDelivery;

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
}