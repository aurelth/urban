using FluentAssertions;
using Urban.Finance.Application.MyMonth;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// Como a conta mora no condomínio, sem ler mês nenhum. Hoje é 7/11/2026. É a mesma regra do mês do morador: com unidade (proprietário
/// ou inquilino) é unit, só como dependente é dependent, sem vínculo é none.
/// </summary>
public class GetMyAccessQueryHandlerTests
{
    private static readonly Guid Resident = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 7);
    private static readonly ResidentUnit Unit101 = new(FakeApportionmentBasis.First.UnitId, FakeApportionmentBasis.First.Block, FakeApportionmentBasis.First.Number);
    private static readonly ResidentUnit Unit102 = new(FakeApportionmentBasis.Second.UnitId, FakeApportionmentBasis.Second.Block, FakeApportionmentBasis.Second.Number);

    private readonly FinanceTestKit _kit = new();

    private GetMyAccessQueryHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new(used.Housing, used.Tenant, used.Clock);
    }

    private Task<Result<MyMonthAccess>> Get(Guid? user = null) => Handler().Handle(new GetMyAccessQuery(user ?? Resident), default);

    [Fact]
    public async Task WithoutACondominium_Fails_AndNobodyIsLookedUp()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<MyMonthAccess> result = await Handler(kit).Handle(new GetMyAccessQuery(Resident), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Housing.Calls.Should().Be(0);
    }

    [Fact]
    public async Task TheHousingIsAskedAboutTheAccountOfTheToken_AsOfTodayInBrasilia()
    {
        _kit.Housing.Lives(Resident, Unit101);

        await Get();

        _kit.Housing.AskedAbout.Should().Be(Resident);
        _kit.Housing.AskedOn.Should().Be(Today);
        _kit.Housing.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SomeoneWhoLivesInAUnit_HasUnitAccess()
    {
        _kit.Housing.Lives(Resident, Unit101);

        (await Get()).Value.Should().Be(MyMonthAccess.Unit);
    }

    [Fact]
    public async Task SomeoneWhoLivesInSeveralUnits_HasUnitAccess()
    {
        _kit.Housing.Lives(Resident, Unit101, Unit102);

        (await Get()).Value.Should().Be(MyMonthAccess.Unit);
    }

    [Fact]
    public async Task ADependentWithoutAUnitOfTheirOwn_HasDependentAccess()
    {
        _kit.Housing.LivesAsDependent(Resident);

        (await Get()).Value.Should().Be(MyMonthAccess.Dependent);
    }

    [Fact]
    public async Task ADependentWhoIsAlsoTheOwnerOfAnotherUnit_HasUnitAccess()
    {
        _kit.Housing.LivesAsDependent(Resident, Unit102);

        (await Get()).Value.Should().Be(MyMonthAccess.Unit);
    }

    [Fact]
    public async Task SomeoneWithoutAnyLink_HasNoAccess()
    {
        (await Get()).Value.Should().Be(MyMonthAccess.None);
    }

    [Fact]
    public async Task AnotherAccountDoesNotInheritTheUnitsOfTheFirst()
    {
        _kit.Housing.Lives(Resident, Unit101);

        (await Get(Guid.CreateVersion7())).Value.Should().Be(MyMonthAccess.None);
    }
}
