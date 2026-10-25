using FluentAssertions;
using Urban.Identity.Infrastructure.Messaging;

namespace Urban.UnitTests.Identity.Infrastructure;

public class WebLinksOptionsTests
{
    [Theory]
    [InlineData("https://urban-pro.io", "https://urban-pro.io/")]
    [InlineData("https://urban-pro.io/", "https://urban-pro.io/")]
    [InlineData("http://localhost:4200", "http://localhost:4200/")]
    [InlineData("https://staging.urban-pro.io/app/", "https://staging.urban-pro.io/app/")]
    public void GetWebBaseUri_ReturnsTheAddressEndingWithASlash(string configured, string expected)
    {
        WebLinksOptions options = new() { WebBaseUrl = configured };

        options.GetWebBaseUri().AbsoluteUri.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("urban-pro.io")]
    [InlineData("/relativo")]
    [InlineData("ftp://urban-pro.io")]
    [InlineData("javascript:alert(1)")]
    public void GetWebBaseUri_WithAnythingButAnAbsoluteHttpUrl_Throws(string configured)
    {
        WebLinksOptions options = new() { WebBaseUrl = configured };

        Action read = () => options.GetWebBaseUri();

        read.Should().Throw<InvalidOperationException>().WithMessage("*Links:WebBaseUrl*");
    }
}