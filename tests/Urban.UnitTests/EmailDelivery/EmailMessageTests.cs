using FluentAssertions;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.EmailDelivery;

public class EmailMessageTests
{
    [Theory]
    [InlineData("sindica@example.com", "example.com")]
    [InlineData("a@b@c.org", "b@c.org")]
    public void RecipientDomain_IsEverythingAfterTheFirstAt(string address, string expected)
    {
        new EmailMessage(address, "assunto", "corpo").RecipientDomain.Should().Be(expected);
    }
}