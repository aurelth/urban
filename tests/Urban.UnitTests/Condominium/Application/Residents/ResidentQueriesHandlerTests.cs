using FluentAssertions;
using Moq;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Application.Residents;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Occupancies;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using Urban.TestSupport.Condominium;

namespace Urban.UnitTests.Condominium.Application.Residents;

/// <summary>As consultas de moradores: a lista (com busca, unidade e página) e um morador.</summary>
public class ResidentQueriesHandlerTests
{
    private readonly ResidentsTestKit _kit = new();

    private ListResidentsQueryHandler ListHandler() => new(_kit.Residents.Object, _kit.Tenant);

    private GetResidentQueryHandler GetHandler() => new(_kit.Residents.Object, _kit.Tenant);

    private void ListReturns(IReadOnlyList<ResidentRecord> items, int total) =>
        _kit.Residents
            .Setup(r => r.ListAsync(It.IsAny<ResidentFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResidents(items, total));

    private ResidentRecord Record(string name = "Ana Lima")
    {
        Person person = _kit.NewResident(name);
        Occupancy occupancy = ResidentBuilders.Occupancy(person, _kit.Unit.Id, OccupancyKind.Tenant, new DateOnly(2025, 2, 1));

        return new ResidentRecord(person, [new ResidentOccupancy(occupancy, "Bloco A", "101")], "6");
    }

    [Fact]
    public async Task List_ReturnsTheResidentsAsTheStoreOrderedThem_WithTheTotalAndThePage()
    {
        ResidentRecord ana = Record("Ana Lima");
        ResidentRecord beto = Record("Beto Rocha");
        ListReturns([ana, beto], total: 7);

        Result<ResidentListing> result = await ListHandler().Handle(new ListResidentsQuery(Page: 2, PageSize: 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.FullName).Should().Equal("Ana Lima", "Beto Rocha");
        result.Value.Items[0].Version.Should().Be("6");
        result.Value.Items[0].Occupancies.Single().Kind.Should().Be(OccupancyKind.Tenant);
        result.Value.Total.Should().Be(7);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task List_TrimsTheSearch_AndPassesTheUnitFilter()
    {
        ListReturns([], 0);

        await ListHandler().Handle(new ListResidentsQuery("  ana ", _kit.Unit.Id, 1, 50), default);

        _kit.Residents.Verify(r => r.ListAsync(new ResidentFilter("ana", _kit.Unit.Id), 1, 50, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task List_TreatsABlankSearchAsNoFilter(string? search)
    {
        ListReturns([], 0);

        await ListHandler().Handle(new ListResidentsQuery(search), default);

        _kit.Residents.Verify(r => r.ListAsync(new ResidentFilter(null, null), 1, 50, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task List_WithAnInvalidPage_FailsWithoutAskingTheStore(int page, int pageSize)
    {
        Result<ResidentListing> result = await ListHandler().Handle(new ListResidentsQuery(Page: page, PageSize: pageSize), default);

        result.Error.Should().Be(ResidentErrors.PageInvalid);
        ResidentErrors.PageInvalid.Code.Should().Be("condominium.resident.page_invalid");
        _kit.Residents.Verify(r => r.ListAsync(It.IsAny<ResidentFilter>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task List_AcceptsTheLargestPage()
    {
        ListReturns([], 0);

        Result<ResidentListing> result = await ListHandler().Handle(new ListResidentsQuery(PageSize: ListResidentsQueryHandler.MaxPageSize), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task List_WithoutACondominium_Fails()
    {
        _kit.WithoutATenant();

        Result<ResidentListing> result = await ListHandler().Handle(new ListResidentsQuery(), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
    }

    [Fact]
    public async Task Get_ReturnsTheResidentWithTheMaskedCpf()
    {
        ResidentRecord record = Record();
        _kit.Existing(record.Person, [.. record.Occupancies]);

        Result<ResidentView> result = await GetHandler().Handle(new GetResidentQuery(record.Person.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(record.Person.Id);
        result.Value.MaskedCpf.Should().Be(record.Person.Cpf.Masked).And.Contain("***");
        result.Value.Version.Should().Be("4");
    }

    [Fact]
    public async Task Get_OfAnUnknownResident_FailsAsNotFound()
    {
        _kit.Residents.Setup(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ResidentRecord?)null);

        Result<ResidentView> result = await GetHandler().Handle(new GetResidentQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(ResidentErrors.NotFound);
        ResidentErrors.NotFound.Code.Should().Be("condominium.resident.not_found");
    }

    [Fact]
    public async Task Get_WithoutACondominium_FailsWithoutAskingTheStore()
    {
        _kit.WithoutATenant();

        Result<ResidentView> result = await GetHandler().Handle(new GetResidentQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(CondominiumErrors.NotFound);
        _kit.Residents.Verify(r => r.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
