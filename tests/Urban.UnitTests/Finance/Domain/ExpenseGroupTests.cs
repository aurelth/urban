using FluentAssertions;
using Urban.Finance.Domain.Expenses;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain;

public class ExpenseGroupTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithAName_StoresItForTheCondominium()
    {
        Result<ExpenseGroup> result = ExpenseGroup.Create(CondominiumId, "Manutenção das garagens", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.CondominiumId.Should().Be(CondominiumId);
        result.Value.Name.Should().Be("Manutenção das garagens");
        result.Value.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_NormalizesTheSpacesInTheName()
    {
        ExpenseGroup.Create(CondominiumId, "  Despesas   ordinárias ", Now).Value.Name.Should().Be("Despesas ordinárias");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Linha\ncom quebra")]
    public void Create_AnInvalidName_Fails(string? name)
    {
        ExpenseGroup.Create(CondominiumId, name, Now).Error.Should().Be(ExpenseGroupErrors.NameInvalid);
    }

    [Fact]
    public void Create_TheNameLimit_IsSixtyCharacters()
    {
        ExpenseGroup.Create(CondominiumId, new string('a', 60), Now).IsSuccess.Should().BeTrue();
        ExpenseGroup.Create(CondominiumId, new string('a', 61), Now).Error.Should().Be(ExpenseGroupErrors.NameInvalid);
    }

    [Fact]
    public void Create_WithoutACondominium_Fails()
    {
        ExpenseGroup.Create(Guid.Empty, "Despesas ordinárias", Now).Error.Should().Be(ExpenseGroupErrors.CondominiumRequired);
    }

    [Fact]
    public void Rename_ChangesTheName_AndAnInvalidNameChangesNothing()
    {
        ExpenseGroup group = ExpenseGroup.Create(CondominiumId, "Garagens", Now).Value;

        group.Rename(" Manutenção das garagens ").IsSuccess.Should().BeTrue();
        group.Name.Should().Be("Manutenção das garagens");

        group.Rename("").Error.Should().Be(ExpenseGroupErrors.NameInvalid);
        group.Name.Should().Be("Manutenção das garagens");
    }

    [Fact]
    public void DefaultName_IsOrdinaryExpenses()
    {
        ExpenseGroup.DefaultName.Should().Be("Despesas ordinárias");
    }

    [Fact]
    public void Create_IsNotPartOfTheReserveFundBaseByDefault()
    {
        ExpenseGroup.Create(CondominiumId, "Manutenção das garagens", Now).Value.ReserveFundBase.Should().BeFalse();
    }

    [Fact]
    public void Create_CanStartAsPartOfTheReserveFundBase()
    {
        ExpenseGroup.Create(CondominiumId, ExpenseGroup.DefaultName, Now, reserveFundBase: true).Value.ReserveFundBase.Should().BeTrue();
    }

    [Fact]
    public void SetReserveFundBase_TurnsTheFlagOnAndOff()
    {
        ExpenseGroup group = ExpenseGroup.Create(CondominiumId, "Obras", Now).Value;

        group.SetReserveFundBase(true);
        group.ReserveFundBase.Should().BeTrue();

        group.SetReserveFundBase(false);
        group.ReserveFundBase.Should().BeFalse();
    }
}
