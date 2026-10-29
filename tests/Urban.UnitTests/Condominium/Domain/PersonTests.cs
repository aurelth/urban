using FluentAssertions;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Domain;

public class PersonTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();

    private static PersonName Name => PersonName.Create("Ana Maria Silva").Value;
    private static Cpf Cpf => Cpf.Create("529.982.247-25").Value;
    private static BirthDate BirthDate => BirthDate.Create(new DateOnly(1985, 3, 12), new DateOnly(2026, 10, 29)).Value;
    private static PhoneNumber Mobile => PhoneNumber.Create("(47) 99123-4567").Value;
    private static Address Address => Address.Create("Rua XV de Novembro", "1234", null, "Centro", "Blumenau", "SC", "89010-000").Value;

    [Fact]
    public void RegisterManager_WithValidData_CreatesThePersonOfTheManager()
    {
        Result<Person> result = Person.RegisterManager(CondominiumId, UserId, Name, Cpf, BirthDate, Mobile, Address, Now);

        result.IsSuccess.Should().BeTrue();
        Person person = result.Value;
        person.Id.Should().NotBeEmpty();
        person.CondominiumId.Should().Be(CondominiumId);
        person.UserId.Should().Be(UserId);
        person.FullName.Should().Be(Name);
        person.Cpf.Should().Be(Cpf);
        person.BirthDate.Should().Be(BirthDate);
        person.Phone.Should().Be(Mobile);
        person.Address.Should().Be(Address);
        person.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void RegisterManager_GivesEachPersonItsOwnId()
    {
        Person first = Person.RegisterManager(CondominiumId, UserId, Name, Cpf, BirthDate, Mobile, Address, Now).Value;
        Person second = Person.RegisterManager(CondominiumId, UserId, Name, Cpf, BirthDate, Mobile, Address, Now).Value;

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void RegisterManager_WithALandline_FailsBecauseTheManagerNeedsAMobile()
    {
        PhoneNumber landline = PhoneNumber.Create("(47) 3322-1100").Value;

        Result<Person> result = Person.RegisterManager(CondominiumId, UserId, Name, Cpf, BirthDate, landline, Address, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PersonErrors.MobileRequired);
        PersonErrors.MobileRequired.Code.Should().Be("condominium.phone.mobile_required");
    }

    [Fact]
    public void RegisterManager_WithoutCondominiumOrUser_Fails()
    {
        Person.RegisterManager(Guid.Empty, UserId, Name, Cpf, BirthDate, Mobile, Address, Now).Error
            .Should().Be(PersonErrors.CondominiumRequired);
        Person.RegisterManager(CondominiumId, Guid.Empty, Name, Cpf, BirthDate, Mobile, Address, Now).Error
            .Should().Be(PersonErrors.UserRequired);
    }
}
