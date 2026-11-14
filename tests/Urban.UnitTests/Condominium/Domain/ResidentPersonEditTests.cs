using FluentAssertions;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>A edição do morador pelo síndico: todos os dados de cadastro, de uma vez.</summary>
public class ResidentPersonEditTests
{
    [Fact]
    public void UpdateResident_ReplacesEveryEditableField()
    {
        Person person = ResidentBuilders.Resident();
        Guid id = person.Id;
        Guid condominiumId = person.CondominiumId;
        DateTimeOffset createdAt = person.CreatedAt;
        PersonName name = PersonName.Create("Maria Souza Lima").Value;
        Cpf cpf = CondominiumBuilders.UniqueCpf();
        BirthDate birthDate = BirthDate.Create(new DateOnly(1988, 1, 2), ResidentBuilders.Today).Value;
        PhoneNumber phone = PhoneNumber.Create("(47) 99111-2222").Value;
        ContactEmail email = ContactEmail.Create("nova@exemplo.com").Value;
        Profession profession = Profession.Create("Médica").Value;
        EmergencyContact contact = EmergencyContact.Create("Paulo Lima", "Esposo", "(47) 99333-4444").Value;

        Result result = person.UpdateResident(name, cpf, birthDate, phone, email, profession, contact);

        result.IsSuccess.Should().BeTrue();
        person.FullName.Should().Be(name);
        person.Cpf.Should().Be(cpf);
        person.BirthDate.Should().Be(birthDate);
        person.Phone.Should().Be(phone);
        person.Email.Should().Be(email);
        person.Profession.Should().Be(profession);
        person.EmergencyContact.Should().Be(contact);
        person.Id.Should().Be(id);
        person.CondominiumId.Should().Be(condominiumId);
        person.CreatedAt.Should().Be(createdAt);
        person.UserId.Should().BeNull();
        person.Address.Should().BeNull();
    }

    [Fact]
    public void UpdateResident_WithoutEmail_ClearsIt()
    {
        Person person = ResidentBuilders.Resident(email: "maria@exemplo.com");

        Result result = person.UpdateResident(person.FullName, person.Cpf, person.BirthDate, person.Phone, null, person.Profession!, person.EmergencyContact!);

        result.IsSuccess.Should().BeTrue();
        person.Email.Should().BeNull();
    }
}
