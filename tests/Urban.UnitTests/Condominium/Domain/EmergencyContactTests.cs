using FluentAssertions;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class EmergencyContactTests
{
    [Fact]
    public void Create_WithValidData_KeepsTheNormalizedParts()
    {
        Result<EmergencyContact> result = EmergencyContact.Create("  Carlos   Souza ", " Irmão ", "(47) 3333-4444");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Value.Should().Be("Carlos Souza");
        result.Value.Relationship.Should().Be("Irmão");
        result.Value.Phone.Value.Should().Be("4733334444");
    }

    [Fact]
    public void Create_AcceptsAMobilePhone()
    {
        EmergencyContact.Create("Carlos Souza", "Irmão", "(47) 99123-4567").IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Carlos")]
    public void Create_WithAnInvalidName_FailsWithTheNameError(string? name)
    {
        Result<EmergencyContact> result = EmergencyContact.Create(name, "Irmão", "(47) 3333-4444");

        result.Error.Should().Be(EmergencyContact.NameInvalid);
        EmergencyContact.NameInvalid.Code.Should().Be("condominium.emergency_contact.name_invalid");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("I")]
    [InlineData("Irmão\u0007")]
    public void Create_WithAnInvalidRelationship_FailsWithTheRelationshipError(string? relationship)
    {
        Result<EmergencyContact> result = EmergencyContact.Create("Carlos Souza", relationship, "(47) 3333-4444");

        result.Error.Should().Be(EmergencyContact.RelationshipInvalid);
        EmergencyContact.RelationshipInvalid.Code.Should().Be("condominium.emergency_contact.relationship_invalid");
    }

    [Fact]
    public void Create_RelationshipBoundaries()
    {
        EmergencyContact.Create("Carlos Souza", new string('a', EmergencyContact.RelationshipMinLength), "(47) 3333-4444").IsSuccess.Should().BeTrue();
        EmergencyContact.Create("Carlos Souza", new string('a', EmergencyContact.RelationshipMaxLength), "(47) 3333-4444").IsSuccess.Should().BeTrue();
        EmergencyContact.Create("Carlos Souza", new string('a', EmergencyContact.RelationshipMaxLength + 1), "(47) 3333-4444").IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public void Create_WithAnInvalidPhone_FailsWithThePhoneError(string? phone)
    {
        Result<EmergencyContact> result = EmergencyContact.Create("Carlos Souza", "Irmão", phone);

        result.Error.Should().Be(EmergencyContact.PhoneInvalid);
        EmergencyContact.PhoneInvalid.Code.Should().Be("condominium.emergency_contact.phone_invalid");
    }
}
