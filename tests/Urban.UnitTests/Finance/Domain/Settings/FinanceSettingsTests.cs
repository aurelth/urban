using FluentAssertions;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Settings;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Domain.Settings;

public class FinanceSettingsTests
{
    private static readonly Guid CondominiumId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddDays(3);

    [Fact]
    public void Create_StoresThePercentageForTheCondominium()
    {
        Result<FinanceSettings> result = FinanceSettings.Create(CondominiumId, 15m, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.CondominiumId.Should().Be(CondominiumId);
        result.Value.ReserveFundPercent.Should().Be(15m);
        result.Value.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_WithoutACondominium_Fails()
    {
        FinanceSettings.Create(Guid.Empty, 15m, Now).Error.Should().Be(ConsumptionErrors.CondominiumRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(12.5)]
    [InlineData(100)]
    public void Create_AcceptsFromZeroToOneHundredWithTwoDecimals(double percent)
    {
        FinanceSettings.Create(CondominiumId, (decimal)percent, Now).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(12.345)]
    public void Create_RefusesOutOfRangeOrTooManyDecimals(double percent)
    {
        FinanceSettings.Create(CondominiumId, (decimal)percent, Now).Error.Should().Be(ConsumptionErrors.ReserveFundPercentInvalid);
    }

    [Fact]
    public void ChangeReserveFundPercent_UpdatesThePercentageAndTheTime()
    {
        FinanceSettings settings = FinanceSettings.Create(CondominiumId, 15m, Now).Value;

        Result result = settings.ChangeReserveFundPercent(10m, Later);

        result.IsSuccess.Should().BeTrue();
        settings.ReserveFundPercent.Should().Be(10m);
        settings.UpdatedAt.Should().Be(Later);
    }

    [Fact]
    public void ChangeReserveFundPercent_WithAnInvalidValue_FailsAndKeepsTheCurrentOne()
    {
        FinanceSettings settings = FinanceSettings.Create(CondominiumId, 15m, Now).Value;

        Result result = settings.ChangeReserveFundPercent(101m, Later);

        result.Error.Should().Be(ConsumptionErrors.ReserveFundPercentInvalid);
        settings.ReserveFundPercent.Should().Be(15m);
        settings.UpdatedAt.Should().Be(Now);
    }
}
