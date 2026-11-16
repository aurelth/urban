using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Consumption;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel.Contracts;

namespace Urban.IntegrationTests.Finance.Persistence;

/// <summary>O que a fatura em PDF lê de uma cobrança: a cobrança, a base do rateio e o total de cada linha do mês.</summary>
[Collection(FinancePostgresCollection.Name)]
public class FinanceChargesTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    private static YearMonth M(int year, int month) => FinanceStoreKit.M(year, month);

    private static UnitCharge Big(MonthClosing closing) => closing.Charges.Single(charge => charge.Number == "101");

    private static UnitCharge Small(MonthClosing closing) => closing.Charges.Single(charge => charge.Number == "102");

    /// <summary>Fecha o mês com dois grupos (Portaria 700 e Limpeza 300), fundo de reserva de 10% sobre a Portaria e água nas duas unidades.</summary>
    private async Task<MonthClosing> StoreClosingWithLinesAsync(YearMonth month)
    {
        Guid doorman = Guid.CreateVersion7();
        Guid cleaning = Guid.CreateVersion7();
        ExpenseEntry[] entries =
        [
            Expense.CreateFixed(_kit.CondominiumId, doorman, "Porteiro", null, month, null, 700m, 10, FinanceStoreKit.Now).Value.GenerateEntry(month, FinanceStoreKit.Now).Value,
            Expense.CreateFixed(_kit.CondominiumId, cleaning, "Faxineira", null, month, null, 300m, 10, FinanceStoreKit.Now).Value.GenerateEntry(month, FinanceStoreKit.Now).Value,
        ];
        ConsumptionTariff tariff = ConsumptionTariff.Create(
            _kit.CondominiumId, month, TariffScale.Create([new TariffBand(10m, 47.43m, 0m), new TariffBand(null, 0m, 8.63m)]).Value, null, 100m, null, FinanceStoreKit.Now).Value;
        MeterReading[] readings =
        [
            MeterReading.Create(_kit.CondominiumId, _kit.Basis.Units[0].UnitId, month, ConsumptionService.Water, 100m, 112m, FinanceStoreKit.Now).Value,
            MeterReading.Create(_kit.CondominiumId, _kit.Basis.Units[1].UnitId, month, ConsumptionService.Water, 50m, 58m, FinanceStoreKit.Now).Value,
        ];
        ClosingOptions options = new(
            new Dictionary<Guid, string> { [doorman] = "Portaria", [cleaning] = "Limpeza" }, new HashSet<Guid> { doorman }, 10m, tariff, readings);
        MonthClosing closing = MonthClosing.Close(
            _kit.CondominiumId, month, month.AddMonths(1).FirstDay.AddDays(9), Guid.CreateVersion7(), FinanceStoreKit.Now, entries, _kit.Basis, options).Value;
        (await _kit.Closings().AddAsync(closing, default)).IsSuccess.Should().BeTrue();

        return closing;
    }

    // ---- A cobrança ----

    [Fact]
    public async Task Find_ReturnsTheChargeWithItsClosingAndTheBasisOfTheApportionment()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(M(2026, 10), new DateOnly(2026, 11, 10));

        ChargeDetail? found = await _kit.Charges().FindAsync(Big(closing).Id, default);

        found.Should().NotBeNull();
        found!.Charge.Id.Should().Be(Big(closing).Id);
        found.Month.Should().Be(M(2026, 10));
        found.DueDate.Should().Be(new DateOnly(2026, 11, 10));
        found.BasisKind.Should().Be(ApportionmentBasisKind.IdealFraction);
        found.TotalWeight.Should().Be(1.0m);
        found.Charge.Weight.Should().Be(0.6m);
    }

    [Fact]
    public async Task Find_OfAnotherCondominiumOrAnUnknownCharge_IsNull()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(M(2026, 10));

        (await _kit.Charges(Guid.CreateVersion7()).FindAsync(Big(closing).Id, default)).Should().BeNull();
        (await _kit.Charges().FindAsync(Guid.CreateVersion7(), default)).Should().BeNull();
    }

    [Fact]
    public async Task Find_TheLineTotalsSumTheLinesOfAllTheChargesOfTheClosing_InTheOrderOfTheCharge()
    {
        MonthClosing closing = await StoreClosingWithLinesAsync(M(2026, 10));
        decimal water = closing.Charges.SelectMany(charge => charge.Lines).Where(line => line.Kind == ChargeLineKind.Water).Sum(line => line.Amount);

        ChargeDetail? found = await _kit.Charges().FindAsync(Big(closing).Id, default);

        found!.LineTotals.Select(total => (total.Kind, total.Label, total.Amount)).Should().Equal(
            (ChargeLineKind.Expenses, "Limpeza", 300m),
            (ChargeLineKind.Expenses, "Portaria", 700m),
            (ChargeLineKind.ReserveFund, "Fundo de reserva", 70m),
            (ChargeLineKind.Water, "Água", water));
        water.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Find_BringsThePercentAndTheBaseOfTheReserveFundOfTheClosing()
    {
        MonthClosing closing = await StoreClosingWithLinesAsync(M(2026, 10));

        ChargeDetail? found = await _kit.Charges().FindAsync(Big(closing).Id, default);

        found!.ReserveFundPercent.Should().Be(10m);
        found.ReserveFundBase.Should().Be(700m);
    }

    [Fact]
    public async Task Find_AClosingWithoutReserveFund_HasNoPercentNorBase()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(M(2026, 10));

        ChargeDetail? found = await _kit.Charges().FindAsync(Big(closing).Id, default);

        found!.ReserveFundPercent.Should().BeNull();
        found.ReserveFundBase.Should().BeNull();
    }

    [Fact]
    public async Task Find_TheLineTotalsOfAClosingNeverIncludeTheOtherMonths()
    {
        await _kit.StoreClosingAsync(M(2026, 9));
        MonthClosing october = await _kit.StoreClosingAsync(M(2026, 10));

        ChargeDetail? found = await _kit.Charges().FindAsync(Big(october).Id, default);

        found!.LineTotals.Select(total => total.Amount).Should().Equal(1000m);
    }
}
