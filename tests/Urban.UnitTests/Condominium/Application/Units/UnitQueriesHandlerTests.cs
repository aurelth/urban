using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Units;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Units;

/// <summary>As consultas de unidades: a lista (com filtros e página), uma unidade e o resumo.</summary>
public class UnitQueriesHandlerTests
{
    private readonly UnitsTestKit _kit = new();

    private ListUnitsQueryHandler ListHandler(UnitsTestKit? kit = null) => new((kit ?? _kit).Units.Object, (kit ?? _kit).Tenant);

    private void ListReturns(IReadOnlyList<UnitRecord> items, int total) =>
        _kit.Units
            .Setup(u => u.ListAsync(It.IsAny<UnitFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedUnits(items, total));

    // ---- Lista ----

    [Fact]
    public async Task List_ReturnsTheUnitsAsTheStoreOrderedThem_WithTheTotalAndThePage()
    {
        UnitRecord a = new(_kit.NewUnit("1", idealFraction: 0.5m), "3");
        UnitRecord b = new(_kit.NewUnit("2", area: 70m), "4");
        ListReturns([a, b], total: 12);

        Result<UnitListing> result = await ListHandler().Handle(new ListUnitsQuery(Page: 2, PageSize: 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.Number).Should().Equal("1", "2");
        result.Value.Items[0].Version.Should().Be("3");
        result.Value.Items[1].Area.Should().Be(70m);
        result.Value.Total.Should().Be(12);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task List_PassesTheFiltersToTheStore_AndActiveUnitsAreTheDefault()
    {
        ListReturns([], 0);

        await ListHandler().Handle(new ListUnitsQuery(Block: "Bloco A", Type: UnitType.House, Page: 1, PageSize: 50), default);

        _kit.Units.Verify(u => u.ListAsync(new UnitFilter("Bloco A", UnitType.House, UnitStatusFilter.Active, null), 1, 50, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(UnitStatusFilter.Deactivated)]
    [InlineData(UnitStatusFilter.All)]
    public async Task List_PassesTheChosenStatus(UnitStatusFilter status)
    {
        ListReturns([], 0);

        await ListHandler().Handle(new ListUnitsQuery(Status: status, Page: 1, PageSize: 50), default);

        _kit.Units.Verify(u => u.ListAsync(It.Is<UnitFilter>(f => f.Status == status), 1, 50, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task List_TrimsTheSearchText_AndTreatsABlankSearchAsNoSearch()
    {
        ListReturns([], 0);

        await ListHandler().Handle(new ListUnitsQuery(Search: "  10 ", Page: 1, PageSize: 50), default);
        await ListHandler().Handle(new ListUnitsQuery(Search: "   ", Page: 1, PageSize: 50), default);

        _kit.Units.Verify(u => u.ListAsync(It.Is<UnitFilter>(f => f.Search == "10"), 1, 50, It.IsAny<CancellationToken>()), Times.Once);
        _kit.Units.Verify(u => u.ListAsync(It.Is<UnitFilter>(f => f.Search == null), 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task List_WithAPageOrASizeOutOfRange_FailsWithoutAskingTheStore(int page, int size)
    {
        Result<UnitListing> result = await ListHandler().Handle(new ListUnitsQuery(Page: page, PageSize: size), default);

        result.Error.Should().Be(UnitErrors.PageInvalid);
        _kit.Units.Verify(u => u.ListAsync(It.IsAny<UnitFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_AcceptsThePageSizeLimit()
    {
        ListReturns([], 0);

        (await ListHandler().Handle(new ListUnitsQuery(Page: 1, PageSize: 200), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task List_BeyondTheLastPage_ReturnsAnEmptyListWithTheTotal()
    {
        ListReturns([], total: 7);

        Result<UnitListing> result = await ListHandler().Handle(new ListUnitsQuery(Page: 9, PageSize: 50), default);

        result.Value.Items.Should().BeEmpty();
        result.Value.Total.Should().Be(7);
    }

    [Fact]
    public async Task List_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        Result<UnitListing> result = await ListHandler(without).Handle(new ListUnitsQuery(Page: 1, PageSize: 50), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    // ---- Uma unidade ----

    [Fact]
    public async Task Get_ReturnsTheUnitWithItsVersion()
    {
        UnitRecord record = _kit.Existing(_kit.NewUnit("101", "Bloco A", 0.02m, 60m), version: "9");

        Result<UnitView> result = await new GetUnitQueryHandler(_kit.Units.Object, _kit.Tenant).Handle(new GetUnitQuery(record.Unit.Id), default);

        result.Value.Id.Should().Be(record.Unit.Id);
        result.Value.Version.Should().Be("9");
        result.Value.Block.Should().Be("Bloco A");
    }

    [Fact]
    public async Task Get_OfAUnitFromAnotherCondominium_IsNotFound_BecauseTheStoreDoesNotSeeIt()
    {
        _kit.Units.Setup(u => u.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((UnitRecord?)null);

        Result<UnitView> result = await new GetUnitQueryHandler(_kit.Units.Object, _kit.Tenant).Handle(new GetUnitQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(UnitErrors.NotFound);
    }

    [Fact]
    public async Task Get_WithoutACondominium_FailsWithCondominiumNotFound()
    {
        UnitsTestKit without = new UnitsTestKit().WithoutATenant();

        Result<UnitView> result = await new GetUnitQueryHandler(without.Units.Object, without.Tenant).Handle(new GetUnitQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }
}
