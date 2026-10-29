using FluentAssertions;
using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class AddressTests
{
    private const string Street = "Rua XV de Novembro";
    private const string Number = "1234";
    private const string Neighborhood = "Centro";
    private const string City = "Blumenau";
    private const string State = "SC";
    private const string PostalCode = "89010-000";

    [Fact]
    public void Create_WithValidData_TrimsAndNormalizesTheFields()
    {
        Result<Address> result = Address.Create("  Rua  XV de   Novembro ", " 1234 ", "  Sala 2 ", "Centro", " Blumenau ", " sc ", "89010-000");

        result.IsSuccess.Should().BeTrue();
        Address address = result.Value;
        address.Street.Should().Be("Rua XV de Novembro");
        address.Number.Should().Be("1234");
        address.Complement.Should().Be("Sala 2");
        address.Neighborhood.Should().Be("Centro");
        address.City.Should().Be("Blumenau");
        address.State.Should().Be("SC");
        address.PostalCode.Value.Should().Be("89010000");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutComplement_KeepsItNull(string? complement)
    {
        Address.Create(Street, Number, complement, Neighborhood, City, State, PostalCode).Value.Complement.Should().BeNull();
    }

    [Fact]
    public void Create_AcceptsNumberWithoutNumeration()
    {
        Address.Create(Street, "S/N", null, Neighborhood, City, State, PostalCode).IsSuccess.Should().BeTrue();
    }

    public static TheoryData<string?, string?, string?, string?, string?, string?, string> InvalidFields => new()
    {
        { null, Number, null, Neighborhood, City, State, "condominium.address.street_invalid" },
        { "  ", Number, null, Neighborhood, City, State, "condominium.address.street_invalid" },
        { new string('a', 121), Number, null, Neighborhood, City, State, "condominium.address.street_invalid" },
        { Street, null, null, Neighborhood, City, State, "condominium.address.number_invalid" },
        { Street, "12345678901", null, Neighborhood, City, State, "condominium.address.number_invalid" },
        { Street, Number, new string('a', 61), Neighborhood, City, State, "condominium.address.complement_invalid" },
        { Street, Number, null, null, City, State, "condominium.address.neighborhood_invalid" },
        { Street, Number, null, new string('a', 81), City, State, "condominium.address.neighborhood_invalid" },
        { Street, Number, null, Neighborhood, "", State, "condominium.address.city_invalid" },
        { Street, Number, null, Neighborhood, new string('a', 81), State, "condominium.address.city_invalid" },
        { Street, Number, null, Neighborhood, City, null, "condominium.address.state_invalid" },
        { Street, Number, null, Neighborhood, City, "XX", "condominium.address.state_invalid" },
        { Street, Number, null, Neighborhood, City, "SCA", "condominium.address.state_invalid" },
        { "Rua\u0007 A", Number, null, Neighborhood, City, State, "condominium.address.street_invalid" },
        { Street, Number, null, Neighborhood, City, State, "condominium.postal_code.invalid" },
    };

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Create_WithInvalidField_FailsWithTheCodeOfThatField(
        string? street,
        string? number,
        string? complement,
        string? neighborhood,
        string? city,
        string? state,
        string expectedCode)
    {
        string postalCode = expectedCode == "condominium.postal_code.invalid" ? "123" : PostalCode;

        Result<Address> result = Address.Create(street, number, complement, neighborhood, city, state, postalCode);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("AC")]
    [InlineData("SP")]
    [InlineData("DF")]
    [InlineData("TO")]
    public void Create_AcceptsEveryStateOfBrazil(string state)
    {
        Address.Create(Street, Number, null, Neighborhood, City, state, PostalCode).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void States_ListsTheTwentySevenFederativeUnits()
    {
        Address.States.Should().HaveCount(27).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Addresses_WithTheSameData_AreEqual()
    {
        Address first = Address.Create(Street, Number, null, Neighborhood, City, State, PostalCode).Value;
        Address second = Address.Create(" " + Street, Number, "", Neighborhood, City, "sc", "89010000").Value;

        first.Should().Be(second);
    }
}
