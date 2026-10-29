using FluentAssertions;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.SharedKernel;

public class PostalCodeLookupResultTests
{
    private static readonly PostalCodeAddress Address = new("01310100", "Avenida Paulista", "Bela Vista", "São Paulo", "SP");

    [Fact]
    public void Found_CarriesTheAddress()
    {
        PostalCodeLookupResult result = PostalCodeLookupResult.Found(Address);

        result.Status.Should().Be(PostalCodeLookupStatus.Found);
        result.Address.Should().Be(Address);
    }

    [Fact]
    public void Found_RefusesANullAddress()
    {
        Func<PostalCodeLookupResult> found = () => PostalCodeLookupResult.Found(null!);

        found.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NotFound_HasNoAddress()
    {
        PostalCodeLookupResult.NotFound.Status.Should().Be(PostalCodeLookupStatus.NotFound);
        PostalCodeLookupResult.NotFound.Address.Should().BeNull();
    }

    [Fact]
    public void Unavailable_HasNoAddress()
    {
        PostalCodeLookupResult.Unavailable.Status.Should().Be(PostalCodeLookupStatus.Unavailable);
        PostalCodeLookupResult.Unavailable.Address.Should().BeNull();
    }
}
