using System.Reflection;
using FluentAssertions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>A edição do condomínio pelo síndico: nome e endereço mudam; o CNPJ e a origem do registro nunca.</summary>
public class CondominiumEditTests
{
    private static readonly Address NewAddress =
        Address.Create("Rua Princesa Isabel", "88", null, "Atiradores", "Joinville", "SC", "89201-000").Value;

    [Fact]
    public void Update_ChangesTheNameAndTheAddress_AndNormalizesTheName()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();

        Result result = condominium.Update("  Residencial   Bela Vista ", NewAddress);

        result.IsSuccess.Should().BeTrue();
        condominium.Name.Should().Be("Residencial Bela Vista");
        condominium.Address.Should().Be(NewAddress);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" A ")]
    [InlineData("Nome com\u0007controle")]
    public void Update_WithAnInvalidName_FailsAndChangesNothing(string? name)
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        string originalName = condominium.Name;
        Address originalAddress = condominium.Address;

        Result result = condominium.Update(name, NewAddress);

        result.Error.Should().Be(CondominiumErrors.NameInvalid);
        condominium.Name.Should().Be(originalName);
        condominium.Address.Should().Be(originalAddress);
    }

    [Fact]
    public void Update_WithANameLongerThanTheLimit_Fails()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();

        condominium.Update(new string('a', 121), NewAddress).Error.Should().Be(CondominiumErrors.NameInvalid);
        condominium.Update(new string('a', 120), NewAddress).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Update_NeverTouchesWhatIdentifiesTheCondominium()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        (Guid id, Guid createdBy, DateTimeOffset createdAt, string cnpj) =
            (condominium.Id, condominium.CreatedBy, condominium.CreatedAt, condominium.Cnpj.Value);

        condominium.Update("Residencial Bela Vista", NewAddress);

        (condominium.Id, condominium.CreatedBy, condominium.CreatedAt, condominium.Cnpj.Value).Should().Be((id, createdBy, createdAt, cnpj));
    }

    [Theory]
    [InlineData(nameof(CondominiumEntity.Id))]
    [InlineData(nameof(CondominiumEntity.Cnpj))]
    [InlineData(nameof(CondominiumEntity.CreatedBy))]
    [InlineData(nameof(CondominiumEntity.CreatedAt))]
    public void TheIdentifyingProperties_HaveNoSetter(string property)
    {
        PropertyInfo info = typeof(CondominiumEntity).GetProperty(property)!;

        info.SetMethod.Should().BeNull("o CNPJ, o Id e a origem do cadastro só mudam por um caminho que não existe para o síndico");
    }

    [Fact]
    public void ThereIsNoPublicWayToChangeTheCnpj()
    {
        IEnumerable<string> methods = typeof(CondominiumEntity)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name);

        methods.Should().NotContain(name => name.Contains("Cnpj", StringComparison.Ordinal));
    }
}
