using FluentAssertions;
using Urban.Finance.Application.Settings;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Settings;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

public class FinanceSettingsHandlersTests
{
    private readonly FinanceTestKit _kit = new();

    private GetFinanceSettingsQueryHandler GetHandler() => new(_kit.Settings, _kit.Tenant);

    private SetReserveFundPercentCommandHandler SetHandler() => new(_kit.Settings, _kit.Tenant, _kit.Clock);

    [Fact]
    public async Task Get_WhenNothingWasConfigured_ReturnsZeroSoThereIsNoReserveFund()
    {
        Result<FinanceSettingsView> result = await GetHandler().Handle(new GetFinanceSettingsQuery(), default);

        result.Value.ReserveFundPercent.Should().Be(0m);
    }

    [Fact]
    public async Task Get_ReturnsTheConfiguredPercentage()
    {
        _kit.Settings.Existing(FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceTestKit.Now).Value);

        (await GetHandler().Handle(new GetFinanceSettingsQuery(), default)).Value.ReserveFundPercent.Should().Be(15m);
    }

    [Fact]
    public async Task Get_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await GetHandler().Handle(new GetFinanceSettingsQuery(), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Set_TheFirstTime_CreatesTheSettingsForTheCondominium()
    {
        Result<FinanceSettingsView> result = await SetHandler().Handle(new SetReserveFundPercentCommand(15m), default);

        result.Value.ReserveFundPercent.Should().Be(15m);
        _kit.Settings.Stored!.CondominiumId.Should().Be(_kit.CondominiumId);
        _kit.Settings.Stored.UpdatedAt.Should().Be(FinanceTestKit.Now);
    }

    [Fact]
    public async Task Set_WhenItAlreadyExists_ChangesTheSameSettings()
    {
        FinanceSettings existing = _kit.Settings.Existing(FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceTestKit.Now.AddDays(-30)).Value);

        Result<FinanceSettingsView> result = await SetHandler().Handle(new SetReserveFundPercentCommand(10m), default);

        result.Value.ReserveFundPercent.Should().Be(10m);
        _kit.Settings.Stored.Should().BeSameAs(existing);
        existing.UpdatedAt.Should().Be(FinanceTestKit.Now);
    }

    [Fact]
    public async Task Set_ZeroTurnsTheReserveFundOff()
    {
        _kit.Settings.Existing(FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceTestKit.Now).Value);

        (await SetHandler().Handle(new SetReserveFundPercentCommand(0m), default)).Value.ReserveFundPercent.Should().Be(0m);
    }

    [Fact]
    public async Task Set_WithAnInvalidPercentage_FailsAndSavesNothing()
    {
        Result<FinanceSettingsView> result = await SetHandler().Handle(new SetReserveFundPercentCommand(150m), default);

        result.Error.Should().Be(ConsumptionErrors.ReserveFundPercentInvalid);
        _kit.Settings.Saves.Should().Be(0);
    }

    [Fact]
    public async Task Set_WithAnInvalidPercentage_KeepsTheCurrentOne()
    {
        _kit.Settings.Existing(FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceTestKit.Now).Value);

        await SetHandler().Handle(new SetReserveFundPercentCommand(-1m), default);

        _kit.Settings.Stored!.ReserveFundPercent.Should().Be(15m);
    }

    [Fact]
    public async Task Set_WithoutATenant_Fails()
    {
        _kit.WithoutATenant();

        (await SetHandler().Handle(new SetReserveFundPercentCommand(15m), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Set_WhenSavingFails_ReturnsTheFailure()
    {
        _kit.Settings.SaveFailure = FinanceErrors.ConcurrencyConflict;

        (await SetHandler().Handle(new SetReserveFundPercentCommand(15m), default)).Error.Should().Be(FinanceErrors.ConcurrencyConflict);
    }
}
