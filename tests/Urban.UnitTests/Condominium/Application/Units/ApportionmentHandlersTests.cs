using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.UnitTests.Condominium.Application.Units;

/// <summary>O método de rateio do condomínio e o resumo que diz se cada método já pode ser usado.</summary>
public class ApportionmentHandlersTests
{
    private readonly UnitsTestKit _kit = new();
    private readonly Mock<IApportionments> _apportionments = new();
    private readonly CondominiumEntity _condominium = CondominiumBuilders.Condominium();
    private readonly List<ChangeLogEntry> _logged = [];

    public ApportionmentHandlersTests()
    {
        _apportionments.Setup(a => a.FindAsync(_kit.CondominiumId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApportionmentSetting(_condominium, "12"));
        _apportionments.Setup(a => a.SaveAsync(It.IsAny<ApportionmentSetting>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .Callback<ApportionmentSetting, ChangeLogEntry, CancellationToken>((_, entry, _) => _logged.Add(entry))
            .ReturnsAsync(Result.Success("13"));
    }

    private ChangeApportionmentMethodCommandHandler Changer() => new(_apportionments.Object, _kit.Tenant, _kit.Atomic, _kit.Clock.Object);

    private GetUnitsSummaryQueryHandler Summarizer(UnitsTestKit? kit = null) =>
        new((kit ?? _kit).Units.Object, _apportionments.Object, (kit ?? _kit).Tenant);

    private void Units(params CondominiumUnit[] units) =>
        _kit.Units.Setup(u => u.AllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(units);

    // ---- Trocar o método ----

    [Fact]
    public async Task Change_SwitchesTheMethod_ReturnsTheNewVersionAndLogsTheFieldName()
    {
        Result<ApportionmentView> result = await Changer().Handle(new ChangeApportionmentMethodCommand(_kit.ActorId, "12", ApportionmentMethod.Area), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Method.Should().Be(ApportionmentMethod.Area);
        result.Value.Version.Should().Be("13");
        _condominium.ApportionmentMethod.Should().Be(ApportionmentMethod.Area);
        ChangeLogEntry entry = _logged.Should().ContainSingle().Subject;
        entry.Kind.Should().Be(ChangeKind.Apportionment);
        entry.CondominiumId.Should().Be(_kit.CondominiumId);
        entry.ActorUserId.Should().Be(_kit.ActorId);
        entry.Fields.Should().Equal("apportionmentMethod");
        entry.At.Should().Be(UnitsTestKit.Now);
    }

    [Fact]
    public async Task Change_WithoutTheVersion_FailsWithVersionRequired()
    {
        Result<ApportionmentView> result = await Changer().Handle(new ChangeApportionmentMethodCommand(_kit.ActorId, null, ApportionmentMethod.Area), default);

        result.Error.Should().Be(CondominiumErrors.VersionRequired);
        _logged.Should().BeEmpty();
    }

    [Fact]
    public async Task Change_WithAnOldVersion_FailsWithAConcurrencyConflict_AndKeepsTheMethod()
    {
        Result<ApportionmentView> result = await Changer().Handle(new ChangeApportionmentMethodCommand(_kit.ActorId, "11", ApportionmentMethod.Area), default);

        result.Error.Should().Be(CondominiumErrors.ConcurrencyConflict);
        _condominium.ApportionmentMethod.Should().Be(ApportionmentMethod.IdealFraction);
    }

    [Fact]
    public async Task Change_ToTheSameMethod_SucceedsWithoutSavingAndWithoutChangingTheVersion()
    {
        Result<ApportionmentView> result = await Changer().Handle(new ChangeApportionmentMethodCommand(_kit.ActorId, "12", ApportionmentMethod.IdealFraction), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be("12");
        _logged.Should().BeEmpty();
        _apportionments.Verify(a => a.SaveAsync(It.IsAny<ApportionmentSetting>(), It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Change_ToAMethodThatDoesNotExist_Fails()
    {
        Result<ApportionmentView> result = await Changer().Handle(new ChangeApportionmentMethodCommand(_kit.ActorId, "12", (ApportionmentMethod)0), default);

        result.Error.Should().Be(CondominiumErrors.ApportionmentMethodInvalid);
        _logged.Should().BeEmpty();
    }

    [Fact]
    public async Task Change_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        Result<ApportionmentView> result = await new ChangeApportionmentMethodCommandHandler(_apportionments.Object, without.Tenant, without.Atomic, without.Clock.Object)
            .Handle(new ChangeApportionmentMethodCommand(without.ActorId, "12", ApportionmentMethod.Area), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    // ---- Resumo ----

    [Fact]
    public async Task Summary_CountsActiveAndDeactivatedUnitsHousesAndApartments()
    {
        Units(
            UnitBuilders.Unit(number: "101"),
            UnitBuilders.Unit(number: "102"),
            UnitBuilders.Unit(number: "Casa 1", type: UnitType.House),
            UnitBuilders.Deactivated(number: "103"));

        Result<UnitsSummary> result = await Summarizer().Handle(new GetUnitsSummaryQuery(), default);

        result.Value.Active.Should().Be(3);
        result.Value.Deactivated.Should().Be(1);
        result.Value.Apartments.Should().Be(2);
        result.Value.Houses.Should().Be(1);
    }

    [Fact]
    public async Task Summary_ShowsTheSumOfTheFractionsAndHowManyUnitsHaveNoFractionOrNoArea()
    {
        Units(
            UnitBuilders.Unit(number: "1", idealFraction: 0.25m, area: 50m),
            UnitBuilders.Unit(number: "2", idealFraction: 0.25m),
            UnitBuilders.Unit(number: "3"),
            UnitBuilders.Deactivated(number: "4", idealFraction: 0.5m));

        UnitsSummary summary = (await Summarizer().Handle(new GetUnitsSummaryQuery(), default)).Value;

        summary.IdealFractionTotal.Should().Be(0.5m);
        summary.WithoutIdealFraction.Should().Be(1);
        summary.WithoutArea.Should().Be(2);
    }

    [Fact]
    public async Task Summary_TellsWhetherEachMethodIsReady()
    {
        Units(
            UnitBuilders.Unit(number: "1", idealFraction: 0.5m, area: 50m),
            UnitBuilders.Unit(number: "2", idealFraction: 0.5m));

        UnitsSummary summary = (await Summarizer().Handle(new GetUnitsSummaryQuery(), default)).Value;

        summary.Readiness.Should().BeEquivalentTo([
            new MethodReadiness(ApportionmentMethod.IdealFraction, true),
            new MethodReadiness(ApportionmentMethod.EqualShare, true),
            new MethodReadiness(ApportionmentMethod.Area, false)]);
    }

    [Fact]
    public async Task Summary_TheIdealFractionIsNotReadyWhileTheSumDoesNotReachOneHundredPercent()
    {
        Units(UnitBuilders.Unit(number: "1", idealFraction: 0.5m), UnitBuilders.Unit(number: "2", idealFraction: 0.4m));

        UnitsSummary summary = (await Summarizer().Handle(new GetUnitsSummaryQuery(), default)).Value;

        summary.Readiness.Single(r => r.Method == ApportionmentMethod.IdealFraction).Ready.Should().BeFalse();
    }

    [Fact]
    public async Task Summary_ShowsTheCurrentMethodAndItsVersion()
    {
        _condominium.ChangeApportionmentMethod(ApportionmentMethod.EqualShare);
        Units();

        UnitsSummary summary = (await Summarizer().Handle(new GetUnitsSummaryQuery(), default)).Value;

        summary.Method.Should().Be(ApportionmentMethod.EqualShare);
        summary.ApportionmentVersion.Should().Be("12");
    }

    [Fact]
    public async Task Summary_OfACondominiumWithoutUnits_HasNoMethodReady()
    {
        Units();

        UnitsSummary summary = (await Summarizer().Handle(new GetUnitsSummaryQuery(), default)).Value;

        summary.Active.Should().Be(0);
        summary.Readiness.Should().OnlyContain(r => !r.Ready);
    }

    [Fact]
    public async Task Summary_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        Result<UnitsSummary> result = await Summarizer(without).Handle(new GetUnitsSummaryQuery(), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }
}
