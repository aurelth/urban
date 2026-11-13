using FluentAssertions;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>O morador como <see cref="Person"/>: nasce sem conta e sem endereço, e a regra do síndico continua igual.</summary>
public class ResidentPersonTests
{
    [Fact]
    public void RegisterResident_CreatesAPersonWithoutAccountAndWithoutAddress()
    {
        Guid condominiumId = Guid.CreateVersion7();
        Cpf cpf = CondominiumBuilders.UniqueCpf();

        Person person = ResidentBuilders.Resident(condominiumId, "Maria Souza", cpf, "maria@exemplo.com");

        person.Id.Should().NotBeEmpty();
        person.CondominiumId.Should().Be(condominiumId);
        person.UserId.Should().BeNull();
        person.Address.Should().BeNull();
        person.FullName.Value.Should().Be("Maria Souza");
        person.Cpf.Should().Be(cpf);
        person.Email!.Value.Should().Be("maria@exemplo.com");
        person.Profession!.Value.Should().Be("Engenheira");
        person.EmergencyContact!.Relationship.Should().Be("Irmão");
        person.CreatedAt.Should().Be(ResidentBuilders.Now);
    }

    [Fact]
    public void RegisterResident_WithoutEmail_KeepsItNull()
    {
        ResidentBuilders.Resident(email: null).Email.Should().BeNull();
    }

    [Fact]
    public void RegisterResident_WithoutACondominium_Fails()
    {
        Result<Person> result = Person.RegisterResident(
            Guid.Empty,
            PersonName.Create("Maria Souza").Value,
            CondominiumBuilders.UniqueCpf(),
            BirthDate.Create(new DateOnly(1990, 5, 20), ResidentBuilders.Today).Value,
            Urban.Condominium.Domain.Contacts.PhoneNumber.Create("(47) 99876-5432").Value,
            null,
            Profession.Create("Engenheira").Value,
            ResidentBuilders.SampleEmergencyContact(),
            ResidentBuilders.Now);

        result.Error.Should().Be(PersonErrors.CondominiumRequired);
    }

    [Fact]
    public void RequireAddress_ForAResident_IsAProgrammingError()
    {
        Person resident = ResidentBuilders.Resident();

        Action act = () => resident.RequireAddress();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequireAddress_ForTheManager_ReturnsTheAddress()
    {
        Person manager = CondominiumBuilders.Manager(Guid.CreateVersion7());

        manager.RequireAddress().Should().Be(manager.Address);
        manager.Email.Should().BeNull();
        manager.Profession.Should().BeNull();
        manager.EmergencyContact.Should().BeNull();
    }
}
