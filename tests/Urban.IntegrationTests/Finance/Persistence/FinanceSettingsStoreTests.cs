using FluentAssertions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Settings;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceSettingsStoreTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    [Fact]
    public async Task Find_BeforeAnythingWasSaved_ReturnsNull()
    {
        (await _kit.Settings().FindAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_ANewSettings_ThenFind_ReturnsIt()
    {
        FinanceSettings settings = FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceStoreKit.Now).Value;

        Result saved = await _kit.Settings().SaveAsync(settings, default);
        FinanceSettings? found = await _kit.Settings().FindAsync(default);

        saved.IsSuccess.Should().BeTrue();
        found.Should().NotBeNull();
        found!.CondominiumId.Should().Be(_kit.CondominiumId);
        found.ReserveFundPercent.Should().Be(15m);
        found.UpdatedAt.Should().Be(FinanceStoreKit.Now);
    }

    [Fact]
    public async Task Save_AChangedSettings_UpdatesTheSameRow()
    {
        await _kit.Settings().SaveAsync(FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceStoreKit.Now).Value, default);
        FinanceSettingsStore store = _kit.Settings();
        FinanceSettings loaded = (await store.FindAsync(default))!;

        loaded.ChangeReserveFundPercent(12.5m, FinanceStoreKit.Now.AddDays(1));
        Result saved = await store.SaveAsync(loaded, default);

        saved.IsSuccess.Should().BeTrue();
        FinanceSettings found = (await _kit.Settings().FindAsync(default))!;
        found.ReserveFundPercent.Should().Be(12.5m);
        found.UpdatedAt.Should().Be(FinanceStoreKit.Now.AddDays(1));
    }

    [Fact]
    public async Task Find_ForAnotherCondominium_ReturnsNull()
    {
        await _kit.Settings().SaveAsync(FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceStoreKit.Now).Value, default);

        (await _kit.Settings(Guid.CreateVersion7()).FindAsync(default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_TwoRequestsCreatingItAtTheSameTime_LetsOneWinAndTheOtherGetsAConflict()
    {
        FinanceSettingsStore first = _kit.Settings();
        FinanceSettingsStore second = _kit.Settings();
        await first.FindAsync(default);
        await second.FindAsync(default);

        Result won = await first.SaveAsync(FinanceSettings.Create(_kit.CondominiumId, 15m, FinanceStoreKit.Now).Value, default);
        Result lost = await second.SaveAsync(FinanceSettings.Create(_kit.CondominiumId, 20m, FinanceStoreKit.Now).Value, default);

        won.IsSuccess.Should().BeTrue();
        lost.Error.Should().Be(FinanceErrors.ConcurrencyConflict);
        (await _kit.Settings().FindAsync(default))!.ReserveFundPercent.Should().Be(15m);
    }
}
