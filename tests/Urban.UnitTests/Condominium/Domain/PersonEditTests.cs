using FluentAssertions;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>A edição dos dados do síndico: o contato (livre) e a identidade (sensível) mudam por caminhos separados.</summary>
public class PersonEditTests
{
    private static readonly Address NewAddress =
        Address.Create("Rua das Flores", "10", "Casa", "Velha", "Blumenau", "SC", "89036-000").Value;

    private static Person Manager() => CondominiumBuilders.Manager(Guid.CreateVersion7());

    [Fact]
    public void UpdateContact_ChangesThePhoneAndTheAddress()
    {
        Person person = Manager();
        PhoneNumber phone = PhoneNumber.Create("(47) 98888-7777").Value;

        Result result = person.UpdateContact(phone, NewAddress);

        result.IsSuccess.Should().BeTrue();
        person.Phone.Should().Be(phone);
        person.Address.Should().Be(NewAddress);
    }

    [Fact]
    public void UpdateContact_WithALandline_FailsAndChangesNothing()
    {
        Person person = Manager();
        PhoneNumber original = person.Phone;
        Address originalAddress = person.Address;

        Result result = person.UpdateContact(PhoneNumber.Create("(47) 3322-1100").Value, NewAddress);

        result.Error.Should().Be(PersonErrors.MobileRequired);
        person.Phone.Should().Be(original);
        person.Address.Should().Be(originalAddress);
    }

    [Fact]
    public void UpdateIdentity_ChangesTheNameTheCpfAndTheBirthDate()
    {
        Person person = Manager();
        PersonName name = PersonName.Create("Ana Paula Souza").Value;
        Cpf cpf = CondominiumBuilders.UniqueCpf();
        BirthDate birthDate = BirthDate.Create(new DateOnly(1990, 6, 1), new DateOnly(2026, 10, 29)).Value;

        person.UpdateIdentity(name, cpf, birthDate);

        person.FullName.Should().Be(name);
        person.Cpf.Should().Be(cpf);
        person.BirthDate.Should().Be(birthDate);
    }

    [Fact]
    public void TheEditMethods_NeverTouchWhoTheRecordBelongsTo()
    {
        Person person = Manager();
        (Guid id, Guid condominiumId, Guid userId, DateTimeOffset createdAt) = (person.Id, person.CondominiumId, person.UserId, person.CreatedAt);

        person.UpdateContact(PhoneNumber.Create("(47) 98888-7777").Value, NewAddress);
        person.UpdateIdentity(PersonName.Create("Ana Paula Souza").Value, CondominiumBuilders.UniqueCpf(), person.BirthDate);

        (person.Id, person.CondominiumId, person.UserId, person.CreatedAt).Should().Be((id, condominiumId, userId, createdAt));
    }
}
