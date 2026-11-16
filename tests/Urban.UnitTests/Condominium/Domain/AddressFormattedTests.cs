using FluentAssertions;
using Urban.Condominium.Domain.Contacts;

namespace Urban.UnitTests.Condominium.Domain;

public class AddressFormattedTests
{
    [Fact]
    public void Formatted_WritesTheAddressInOneLine()
    {
        Address address = Address.Create("Rua das Flores", "100", null, "Centro", "Blumenau", "SC", "89010-000").Value;

        address.Formatted.Should().Be("Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000");
    }

    [Fact]
    public void Formatted_PutsTheComplementRightAfterTheNumber()
    {
        Address address = Address.Create("Rua das Flores", "100", "Bloco B", "Centro", "Blumenau", "SC", "89010-000").Value;

        address.Formatted.Should().Be("Rua das Flores, 100, Bloco B - Centro - Blumenau/SC - 89010-000");
    }
}
