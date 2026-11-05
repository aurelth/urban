using FluentAssertions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.UnitTests.Condominium.Domain;

/// <summary>As regras puras das unidades: identidade, ordem natural, soma das frações e método de rateio.</summary>
public class UnitRulesTests
{
    // ---- Identidade ----

    [Fact]
    public void Identity_IsTheBlockPlusTheNumber_IgnoringCase()
    {
        UnitIdentity a = UnitBuilders.Unit(block: "Bloco A", number: "Casa 7").Identity;
        UnitIdentity b = UnitBuilders.Unit(block: "BLOCO a", number: "CASA 7").Identity;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Identity_TheSameNumberInDifferentBlocksAreDifferentUnits()
    {
        UnitIdentity a = UnitBuilders.Unit(block: "A", number: "101").Identity;
        UnitIdentity b = UnitBuilders.Unit(block: "B", number: "101").Identity;

        a.Should().NotBe(b);
    }

    [Fact]
    public void Identity_NoBlockAndBlockAreDifferentUnits_AndAnEmptyBlockIsNoBlock()
    {
        UnitIdentity withoutBlock = UnitBuilders.Unit(block: null, number: "101").Identity;
        UnitIdentity withBlock = UnitBuilders.Unit(block: "Bloco A", number: "101").Identity;

        withoutBlock.Should().NotBe(withBlock);
        new UnitIdentity("", "101").Should().Be(withoutBlock);
        new UnitIdentity(null, "101").Should().Be(withoutBlock);
    }

    // ---- Ordem natural ----

    private static List<string> Sorted(params (string? Block, string Number)[] units) =>
        [.. units
            .Select(u => UnitBuilders.Unit(block: u.Block, number: u.Number))
            .Order(UnitOrder.Comparer)
            .Select(u => $"{u.Block}|{u.Number}")];

    [Fact]
    public void Order_PutsTwoBeforeTenAndNotAfterOne()
    {
        Sorted((null, "10"), (null, "2"), (null, "1"), (null, "20")).Should().Equal("|1", "|2", "|10", "|20");
    }

    [Fact]
    public void Order_PutsHouseTwoBeforeHouseTen()
    {
        Sorted((null, "Casa 10"), (null, "Casa 2"), (null, "Casa 1")).Should().Equal("|Casa 1", "|Casa 2", "|Casa 10");
    }

    [Fact]
    public void Order_ComparesTheBlockFirstAndThenTheNumber()
    {
        Sorted(("Bloco 10", "1"), ("Bloco 2", "5"), ("Bloco 2", "1"), (null, "99"))
            .Should().Equal("|99", "Bloco 2|1", "Bloco 2|5", "Bloco 10|1");
    }

    [Fact]
    public void Order_IgnoresCaseAndKeepsTheSameResultWhateverTheInputOrder()
    {
        List<string> first = Sorted((null, "b1"), (null, "A2"), (null, "a10"));
        List<string> second = Sorted((null, "a10"), (null, "b1"), (null, "A2"));

        first.Should().Equal("|A2", "|a10", "|b1");
        second.Should().Equal(first);
    }

    // ---- Soma das frações ----

    [Fact]
    public void FractionTotal_OfActiveUnits_ExactlyOneHundredPercentIsAccepted()
    {
        CondominiumUnit[] units = [UnitBuilders.Unit(number: "1", idealFraction: 0.6m), UnitBuilders.Unit(number: "2", idealFraction: 0.4m)];

        decimal total = IdealFractions.TotalOf(units);

        total.Should().Be(1m);
        IdealFractions.EnsureWithinWhole(total).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void FractionTotal_AboveOneHundredPercentIsRefused()
    {
        Result result = IdealFractions.EnsureWithinWhole(1.00000001m);

        result.Error.Should().Be(UnitErrors.FractionSumExceeded);
    }

    [Fact]
    public void FractionTotal_IgnoresDeactivatedUnitsAndUnitsWithoutAFraction()
    {
        CondominiumUnit[] units =
        [
            UnitBuilders.Unit(number: "1", idealFraction: 0.5m),
            UnitBuilders.Deactivated(number: "2", idealFraction: 0.9m),
            UnitBuilders.Unit(number: "3"),
        ];

        IdealFractions.TotalOf(units).Should().Be(0.5m);
    }

    [Fact]
    public void FractionTotal_IsExactInDecimal_SoThreeThirdsDoNotOverflow()
    {
        CondominiumUnit[] units = [.. Enumerable.Range(1, 3).Select(i => UnitBuilders.Unit(number: $"{i}", idealFraction: 0.33333333m))];

        decimal total = IdealFractions.TotalOf(units);

        total.Should().Be(0.99999999m);
        IdealFractions.EnsureWithinWhole(total).IsSuccess.Should().BeTrue();
    }

    // ---- Método de rateio ----

    [Fact]
    public void ANewCondominium_StartsWithTheIdealFractionApportionment()
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();

        condominium.ApportionmentMethod.Should().Be(ApportionmentMethod.IdealFraction);
    }

    [Theory]
    [InlineData(ApportionmentMethod.EqualShare)]
    [InlineData(ApportionmentMethod.Area)]
    [InlineData(ApportionmentMethod.IdealFraction)]
    public void ChangeApportionmentMethod_SwitchesToTheChosenMethod(ApportionmentMethod method)
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();

        Result result = condominium.ChangeApportionmentMethod(method);

        result.IsSuccess.Should().BeTrue();
        condominium.ApportionmentMethod.Should().Be(method);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void ChangeApportionmentMethod_RefusesAMethodThatDoesNotExistAndKeepsTheCurrentOne(int method)
    {
        CondominiumEntity condominium = CondominiumBuilders.Condominium();
        condominium.ChangeApportionmentMethod(ApportionmentMethod.Area);

        Result result = condominium.ChangeApportionmentMethod((ApportionmentMethod)method);

        result.Error.Should().Be(CondominiumErrors.ApportionmentMethodInvalid);
        condominium.ApportionmentMethod.Should().Be(ApportionmentMethod.Area);
    }

    // ---- Prontidão de cada método ----

    [Fact]
    public void Readiness_IdealFractionNeedsEveryActiveUnitWithAFractionAndTheSumAtOneHundredPercent()
    {
        CondominiumUnit[] complete = [UnitBuilders.Unit(number: "1", idealFraction: 0.5m), UnitBuilders.Unit(number: "2", idealFraction: 0.5m)];
        CondominiumUnit[] incomplete = [UnitBuilders.Unit(number: "1", idealFraction: 0.5m), UnitBuilders.Unit(number: "2", idealFraction: 0.4m)];
        CondominiumUnit[] missing = [UnitBuilders.Unit(number: "1", idealFraction: 1m), UnitBuilders.Unit(number: "2")];

        ApportionmentReadiness.IsReady(ApportionmentMethod.IdealFraction, complete).Should().BeTrue();
        ApportionmentReadiness.IsReady(ApportionmentMethod.IdealFraction, incomplete).Should().BeFalse();
        ApportionmentReadiness.IsReady(ApportionmentMethod.IdealFraction, missing).Should().BeFalse();
    }

    [Fact]
    public void Readiness_EqualShareNeedsAtLeastOneActiveUnit()
    {
        ApportionmentReadiness.IsReady(ApportionmentMethod.EqualShare, [UnitBuilders.Unit()]).Should().BeTrue();
        ApportionmentReadiness.IsReady(ApportionmentMethod.EqualShare, [UnitBuilders.Deactivated()]).Should().BeFalse();
    }

    [Fact]
    public void Readiness_AreaNeedsEveryActiveUnitWithAnArea()
    {
        ApportionmentReadiness.IsReady(ApportionmentMethod.Area, [UnitBuilders.Unit(area: 50m), UnitBuilders.Unit(number: "2", area: 60m)]).Should().BeTrue();
        ApportionmentReadiness.IsReady(ApportionmentMethod.Area, [UnitBuilders.Unit(area: 50m), UnitBuilders.Unit(number: "2")]).Should().BeFalse();
    }

    [Theory]
    [InlineData(ApportionmentMethod.IdealFraction)]
    [InlineData(ApportionmentMethod.EqualShare)]
    [InlineData(ApportionmentMethod.Area)]
    public void Readiness_NoMethodIsReadyWithoutUnits(ApportionmentMethod method)
    {
        ApportionmentReadiness.IsReady(method, []).Should().BeFalse();
    }
}
