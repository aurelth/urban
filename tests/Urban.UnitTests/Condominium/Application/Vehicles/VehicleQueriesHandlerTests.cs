using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Vehicles;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Units;
using Urban.Condominium.Domain.Vehicles;
using Urban.SharedKernel;

namespace Urban.UnitTests.Condominium.Application.Vehicles;

public class VehicleQueriesHandlerTests
{
    private readonly VehiclesTestKit _kit = new();

    // ---- Um veículo ----

    [Fact]
    public async Task Get_ReturnsTheVehicleWithItsUnitAndVersion()
    {
        CondominiumUnit unit = _kit.ActiveUnit("101", "Bloco A");
        VehicleRecord record = _kit.Existing(unit, version: "9");

        Result<VehicleView> result = await new GetVehicleQueryHandler(_kit.Vehicles.Object, _kit.Tenant).Handle(new GetVehicleQuery(record.Vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(record.Vehicle.Id);
        result.Value.UnitBlock.Should().Be("Bloco A");
        result.Value.UnitNumber.Should().Be("101");
        result.Value.Version.Should().Be("9");
    }

    [Fact]
    public async Task Get_WithAVehicleThatDoesNotExistOrBelongsToAnotherCondominium_FailsWithNotFound() =>
        (await new GetVehicleQueryHandler(_kit.Vehicles.Object, _kit.Tenant).Handle(new GetVehicleQuery(Guid.CreateVersion7()), default))
            .Error.Should().Be(VehicleErrors.NotFound);

    [Fact]
    public async Task Get_WithoutACondominium_FailsWithNotFound() =>
        (await new GetVehicleQueryHandler(_kit.Vehicles.Object, _kit.WithoutATenant().Tenant).Handle(new GetVehicleQuery(Guid.CreateVersion7()), default))
            .Error.Should().Be(CondominiumErrors.NotFound);

    // ---- A lista ----

    private ListVehiclesQueryHandler ListHandler() => new(_kit.Vehicles.Object, _kit.Tenant);

    [Fact]
    public async Task List_AsksForTheRequestedPageWithTheFilterTrimmed_AndMapsTheViews()
    {
        CondominiumUnit unit = _kit.ActiveUnit("101", "Bloco A");
        Guid other = Guid.CreateVersion7();
        VehicleRecord first = _kit.Existing(unit, "ABC1D23");
        VehicleRecord second = _kit.Existing(unit, "DEF4567", "Gol", "Preto");
        _kit.Vehicles
            .Setup(v => v.ListAsync(new VehicleFilter(other, "argo"), 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedVehicles([first, second], 12));

        Result<VehicleListing> result = await ListHandler().Handle(new ListVehiclesQuery(other, "  argo ", 2, 10), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Total.Should().Be(12);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(10);
        result.Value.Items.Select(i => i.Plate).Should().Equal("ABC1D23", "DEF4567");
    }

    [Fact]
    public async Task List_WithABlankSearch_AsksWithoutAFilter()
    {
        _kit.Vehicles
            .Setup(v => v.ListAsync(new VehicleFilter(null, null), 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedVehicles([], 0));

        Result<VehicleListing> result = await ListHandler().Handle(new ListVehiclesQuery(null, "   "), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task List_WithAPageOutsideTheLimits_FailsWithPageInvalid(int page, int pageSize)
    {
        Result<VehicleListing> result = await ListHandler().Handle(new ListVehiclesQuery(null, null, page, pageSize), default);

        result.Error.Should().Be(VehicleErrors.PageInvalid);
        _kit.Vehicles.Verify(v => v.ListAsync(It.IsAny<VehicleFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_WithoutACondominium_FailsWithNotFound() =>
        (await new ListVehiclesQueryHandler(_kit.Vehicles.Object, _kit.WithoutATenant().Tenant).Handle(new ListVehiclesQuery(), default))
            .Error.Should().Be(CondominiumErrors.NotFound);
}
