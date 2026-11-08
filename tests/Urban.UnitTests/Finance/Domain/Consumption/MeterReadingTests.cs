using FluentAssertions;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Months;

namespace Urban.UnitTests.Finance.Domain.Consumption;

public class MeterReadingTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly Guid UnitId = Guid.CreateVersion7();
    private static readonly YearMonth September = YearMonth.Create(2026, 9).Value;
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_KeepsTheReadingsAndComputesTheConsumption()
    {
        MeterReading reading = MeterReading.Create(CondominiumId, UnitId, September, ConsumptionService.Water, 1551.913m, 1569.562m, Now).Value;

        reading.CondominiumId.Should().Be(CondominiumId);
        reading.UnitId.Should().Be(UnitId);
        reading.Month.Should().Be(September);
        reading.Service.Should().Be(ConsumptionService.Water);
        reading.Previous.Should().Be(1551.913m);
        reading.Current.Should().Be(1569.562m);
        reading.Consumption.Should().Be(17.649m);
        reading.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_EqualReadingsMeanNoConsumption()
    {
        MeterReading.Create(CondominiumId, UnitId, September, ConsumptionService.Gas, 122.151m, 122.151m, Now).Value.Consumption.Should().Be(0m);
    }

    [Fact]
    public void Create_ACurrentReadingBelowThePreviousOneFails()
    {
        MeterReading.Create(CondominiumId, UnitId, September, ConsumptionService.Water, 100m, 99.999m, Now).Error
            .Should().Be(ConsumptionErrors.ReadingInvalid);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(1.2345, 5)]
    [InlineData(1, 5.0005)]
    [InlineData(1, 10000000)]
    public void Create_RejectsNegativeValuesTooManyDecimalsAndHugeNumbers(double previous, double current)
    {
        MeterReading.Create(CondominiumId, UnitId, September, ConsumptionService.Water, (decimal)previous, (decimal)current, Now).Error
            .Should().Be(ConsumptionErrors.ReadingInvalid);
    }

    [Fact]
    public void Create_RequiresTheCondominiumAndTheUnit()
    {
        MeterReading.Create(Guid.Empty, UnitId, September, ConsumptionService.Water, 1m, 2m, Now).IsFailure.Should().BeTrue();
        MeterReading.Create(CondominiumId, Guid.Empty, September, ConsumptionService.Water, 1m, 2m, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Correct_ChangesBothReadingsAndRevalidates()
    {
        MeterReading reading = MeterReading.Create(CondominiumId, UnitId, September, ConsumptionService.Water, 1m, 2m, Now).Value;

        reading.Correct(10m, 25.5m).IsSuccess.Should().BeTrue();
        reading.Consumption.Should().Be(15.5m);

        reading.Correct(30m, 20m).Error.Should().Be(ConsumptionErrors.ReadingInvalid);
        reading.Consumption.Should().Be(15.5m);
    }
}
