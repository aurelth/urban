using FluentAssertions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Contacts;
using Urban.Condominium.Domain.Documents;
using Urban.SharedKernel;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

public class CondominiumTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ManagerId = Guid.CreateVersion7();

    private static Cnpj Cnpj => Cnpj.Create("11.222.333/0001-81").Value;
    private static Address Address => Address.Create("Rua XV de Novembro", "1234", null, "Centro", "Blumenau", "SC", "89010-000").Value;

    [Fact]
    public void Register_WithValidData_CreatesTheCondominium()
    {
        Result<CondominiumEntity> result = CondominiumEntity.Register("  Residencial   Vila Verde ", Cnpj, Address, ManagerId, Now);

        result.IsSuccess.Should().BeTrue();
        CondominiumEntity condominium = result.Value;
        condominium.Id.Should().NotBeEmpty();
        condominium.Name.Should().Be("Residencial Vila Verde");
        condominium.Cnpj.Should().Be(Cnpj);
        condominium.Address.Should().Be(Address);
        condominium.CreatedBy.Should().Be(ManagerId);
        condominium.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void Register_WithoutAName_Fails(string? name)
    {
        Result<CondominiumEntity> result = CondominiumEntity.Register(name, Cnpj, Address, ManagerId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CondominiumErrors.NameInvalid);
        CondominiumErrors.NameInvalid.Code.Should().Be("condominium.name.invalid");
    }

    [Fact]
    public void Register_WithControlCharactersInTheName_Fails()
    {
        CondominiumEntity.Register("Vila\u0007 Verde", Cnpj, Address, ManagerId, Now).Error
            .Should().Be(CondominiumErrors.NameInvalid);
    }

    [Fact]
    public void Register_AcceptsANameOfUpTo120Characters_AndRefusesMore()
    {
        CondominiumEntity.Register(new string('a', 120), Cnpj, Address, ManagerId, Now).IsSuccess.Should().BeTrue();
        CondominiumEntity.Register(new string('a', 121), Cnpj, Address, ManagerId, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Register_WithoutTheManager_Fails()
    {
        CondominiumEntity.Register("Vila Verde", Cnpj, Address, Guid.Empty, Now).Error
            .Should().Be(CondominiumErrors.ManagerRequired);
    }

    [Fact]
    public void Register_GivesEachCondominiumItsOwnId()
    {
        CondominiumEntity first = CondominiumEntity.Register("Vila Verde", Cnpj, Address, ManagerId, Now).Value;
        CondominiumEntity second = CondominiumEntity.Register("Vila Verde", Cnpj, Address, ManagerId, Now).Value;

        first.Id.Should().NotBe(second.Id);
    }
}
