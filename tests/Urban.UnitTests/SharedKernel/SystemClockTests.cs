using FluentAssertions;
using Urban.SharedKernel.Time;

namespace Urban.UnitTests.SharedKernel;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsTheCurrentInstantInUtc()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        DateTimeOffset now = new SystemClock().UtcNow;

        now.Offset.Should().Be(TimeSpan.Zero);
        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }
}
