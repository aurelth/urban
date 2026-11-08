using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.UnitTests.Condominium.Application.Units;

/// <summary>O contrato que o Financeiro usa para ratear: as unidades ativas, na ordem natural, cada uma com o peso do método escolhido.</summary>
public class ApportionmentBasisTests
{
    private readonly UnitsTestKit _kit = new();
    private readonly Mock<IApportionments> _apportionments = new();
    private readonly CondominiumEntity _condominium = CondominiumBuilders.Condominium();

    public ApportionmentBasisTests()
    {
        _apportionments.Setup(a => a.FindAsync(_kit.CondominiumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApportionmentSetting(_condominium, "1"));
    }

    private ApportionmentBasisProvider Provider(UnitsTestKit? kit = null) => new((kit ?? _kit).Units.Object, _apportionments.Object, (kit ?? _kit).Tenant);

    private void Units(params CondominiumUnit[] units) =>
        _kit.Units.Setup(u => u.AllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(units);

    [Fact]
    public async Task Get_ByIdealFraction_GivesEachActiveUnitItsFractionAsWeight()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.IdealFraction);
        CondominiumUnit first = _kit.NewUnit("101", idealFraction: 0.6m);
        CondominiumUnit second = _kit.NewUnit("102", idealFraction: 0.4m);
        Units(first, second);

        Result<ApportionmentBasis> result = await Provider().GetAsync(default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Kind.Should().Be(ApportionmentBasisKind.IdealFraction);
        result.Value.Units.Should().Equal(
            new ApportionmentUnit(first.Id, null, "101", 0.6m),
            new ApportionmentUnit(second.Id, null, "102", 0.4m));
    }

    [Fact]
    public async Task Get_ByEqualShare_GivesEveryActiveUnitTheSameWeight()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.EqualShare);
        CondominiumUnit first = _kit.NewUnit("101");
        CondominiumUnit second = _kit.NewUnit("102", "A");
        Units(first, second);

        Result<ApportionmentBasis> result = await Provider().GetAsync(default);

        result.Value.Kind.Should().Be(ApportionmentBasisKind.EqualShare);
        result.Value.Units.Select(u => u.Weight).Should().Equal(1m, 1m);
        result.Value.Units[1].Block.Should().Be("A");
    }

    [Fact]
    public async Task Get_ByArea_GivesEachActiveUnitItsAreaAsWeight()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.Area);
        Units(_kit.NewUnit("101", area: 55.5m), _kit.NewUnit("102", area: 80m));

        Result<ApportionmentBasis> result = await Provider().GetAsync(default);

        result.Value.Kind.Should().Be(ApportionmentBasisKind.Area);
        result.Value.Units.Select(u => u.Weight).Should().Equal(55.5m, 80m);
    }

    [Fact]
    public async Task Get_IgnoresDeactivatedUnits()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.EqualShare);
        CondominiumUnit active = _kit.NewUnit("101");
        Units(active, UnitBuilders.Deactivated(_kit.CondominiumId, "102"));

        Result<ApportionmentBasis> result = await Provider().GetAsync(default);

        result.Value.Units.Should().ContainSingle().Which.UnitId.Should().Be(active.Id);
    }

    [Fact]
    public async Task Get_WhenTheDataDoesNotCoverTheMethod_FailsAsNotReady()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.IdealFraction);
        Units(_kit.NewUnit("101", idealFraction: 0.5m), _kit.NewUnit("102"));

        Result<ApportionmentBasis> result = await Provider().GetAsync(default);

        result.Error.Should().Be(CondominiumErrors.ApportionmentNotReady);
    }

    [Fact]
    public async Task Get_WithTheFractionsNotAddingUp_FailsAsNotReady()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.IdealFraction);
        Units(_kit.NewUnit("101", idealFraction: 0.5m), _kit.NewUnit("102", idealFraction: 0.3m));

        (await Provider().GetAsync(default)).Error.Should().Be(CondominiumErrors.ApportionmentNotReady);
    }

    [Fact]
    public async Task Get_WithoutActiveUnits_FailsAsNotReady()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.EqualShare);
        Units(UnitBuilders.Deactivated(_kit.CondominiumId, "101"));

        (await Provider().GetAsync(default)).Error.Should().Be(CondominiumErrors.ApportionmentNotReady);
    }

    [Fact]
    public async Task Get_WithoutACondominium_FailsAsNotFound()
    {
        Result<ApportionmentBasis> result = await Provider(new UnitsTestKit().WithoutATenant()).GetAsync(default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Get_WhenTheCondominiumRowIsMissing_FailsAsNotFound()
    {
        _apportionments.Setup(a => a.FindAsync(_kit.CondominiumId, It.IsAny<CancellationToken>())).ReturnsAsync((ApportionmentSetting?)null);

        (await Provider().GetAsync(default)).Error.Should().Be(CondominiumErrors.NotFound);
    }
}
