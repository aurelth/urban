using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinanceInvoiceDeliveriesTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    private static YearMonth October => FinanceStoreKit.M(2026, 10);

    private static UnitCharge Big(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).First();

    private static UnitCharge Small(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).Last();

    private InvoiceDelivery Queued(UnitCharge charge, Guid? condominiumId = null) =>
        InvoiceDelivery.Queue(condominiumId ?? _kit.CondominiumId, charge.Id, Guid.CreateVersion7(), FinanceStoreKit.Now);

    // ---- Cobranças do mês ----

    [Fact]
    public async Task FindCharge_ReturnsTheUnitTheMonthTheDueDateAndTheAmount()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        UnitCharge charge = Small(closing);

        ClosingCharge? found = await _kit.InvoiceDeliveries().FindChargeAsync(charge.Id, default);

        found.Should().Be(new ClosingCharge(charge.Id, charge.UnitId, "A", "102", October, closing.DueDate, 400m));
    }

    [Fact]
    public async Task FindCharge_OfAnotherCondominiumOrThatDoesNotExist_IsNotFound()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);

        (await _kit.InvoiceDeliveries(Guid.CreateVersion7()).FindChargeAsync(Big(closing).Id, default)).Should().BeNull();
        (await _kit.InvoiceDeliveries().FindChargeAsync(Guid.CreateVersion7(), default)).Should().BeNull();
    }

    [Fact]
    public async Task ListCharges_ReturnsTheChargesOfTheMonthInTheOrderOfTheUnits()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);

        IReadOnlyList<ClosingCharge>? charges = await _kit.InvoiceDeliveries().ListChargesAsync(October, default);

        charges.Should().NotBeNull();
        charges!.Select(charge => charge.Number).Should().Equal("101", "102");
        charges.Select(charge => charge.ChargeId).Should().Equal(Big(closing).Id, Small(closing).Id);
        charges.Should().OnlyContain(charge => charge.Month == October && charge.DueDate == closing.DueDate);
        charges.Select(charge => charge.Amount).Should().Equal(600m, 400m);
    }

    [Fact]
    public async Task ListCharges_OfAMonthNotClosedOrOfAnotherCondominium_IsNull()
    {
        await _kit.StoreClosingAsync(October);

        (await _kit.InvoiceDeliveries().ListChargesAsync(FinanceStoreKit.M(2026, 9), default)).Should().BeNull();
        (await _kit.InvoiceDeliveries(Guid.CreateVersion7()).ListChargesAsync(October, default)).Should().BeNull();
    }

    // ---- Envios ----

    [Fact]
    public async Task Add_ThenFind_KeepsEveryField()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        InvoiceDelivery delivery = Queued(Big(closing));
        delivery.MarkFailed(InvoiceDeliveryFailure.NotDelivered, FinanceStoreKit.Now.AddMinutes(1));

        (await _kit.InvoiceDeliveries().AddAsync([delivery], default)).IsSuccess.Should().BeTrue();
        InvoiceDelivery? found = await _kit.InvoiceDeliveries().FindAsync(Big(closing).Id, default);

        found.Should().NotBeNull();
        found!.Id.Should().Be(delivery.Id);
        found.CondominiumId.Should().Be(_kit.CondominiumId);
        found.ChargeId.Should().Be(Big(closing).Id);
        found.Status.Should().Be(InvoiceDeliveryStatus.Failed);
        found.Attempts.Should().Be(1);
        found.RequestedBy.Should().Be(delivery.RequestedBy);
        found.RequestedAt.Should().Be(FinanceStoreKit.Now);
        found.LastAttemptAt.Should().Be(FinanceStoreKit.Now.AddMinutes(1));
        found.SentAt.Should().BeNull();
        found.FailureReason.Should().Be("not_delivered");
    }

    [Fact]
    public async Task Find_OfAChargeWithoutADelivery_OrOfAnotherCondominium_IsNull()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        (await _kit.InvoiceDeliveries().AddAsync([Queued(Big(closing))], default)).IsSuccess.Should().BeTrue();

        (await _kit.InvoiceDeliveries().FindAsync(Small(closing).Id, default)).Should().BeNull();
        (await _kit.InvoiceDeliveries(Guid.CreateVersion7()).FindAsync(Big(closing).Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task List_ReturnsTheDeliveriesOfTheGivenChargesByChargeId()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        InvoiceDelivery first = Queued(Big(closing));
        (await _kit.InvoiceDeliveries().AddAsync([first], default)).IsSuccess.Should().BeTrue();

        IReadOnlyDictionary<Guid, InvoiceDelivery> found =
            await _kit.InvoiceDeliveries().ListAsync([Big(closing).Id, Small(closing).Id], default);

        found.Keys.Should().Equal(Big(closing).Id);
        found[Big(closing).Id].Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task List_NeverShowsTheDeliveriesOfAnotherCondominium()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        (await _kit.InvoiceDeliveries().AddAsync([Queued(Big(closing))], default)).IsSuccess.Should().BeTrue();

        (await _kit.InvoiceDeliveries(Guid.CreateVersion7()).ListAsync([Big(closing).Id], default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Add_AChargeThatAlreadyHasADelivery_FailsAndKeepsTheFirst()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        InvoiceDelivery first = Queued(Big(closing));
        (await _kit.InvoiceDeliveries().AddAsync([first], default)).IsSuccess.Should().BeTrue();

        Result second = await _kit.InvoiceDeliveries().AddAsync([Queued(Big(closing))], default);

        second.Error.Should().Be(InvoiceDeliveryErrors.AlreadyQueued);
        (await _kit.InvoiceDeliveries().FindAsync(Big(closing).Id, default))!.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task Add_ABatchWithOneRepeatedCharge_StoresNothingOfTheBatch()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        (await _kit.InvoiceDeliveries().AddAsync([Queued(Big(closing))], default)).IsSuccess.Should().BeTrue();

        Result batch = await _kit.InvoiceDeliveries().AddAsync([Queued(Small(closing)), Queued(Big(closing))], default);

        batch.Error.Should().Be(InvoiceDeliveryErrors.AlreadyQueued);
        (await _kit.InvoiceDeliveries().FindAsync(Small(closing).Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_StoresWhatTheDomainChanged()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        InvoiceDelivery delivery = Queued(Big(closing));
        (await _kit.InvoiceDeliveries().AddAsync([delivery], default)).IsSuccess.Should().BeTrue();
        InvoiceDelivery stored = (await _kit.InvoiceDeliveries().FindAsync(Big(closing).Id, default))!;
        Guid other = Guid.CreateVersion7();

        stored.MarkSent(FinanceStoreKit.Now.AddMinutes(2));
        await _kit.InvoiceDeliveries().SaveAsync(stored, default);
        stored.Requeue(other, FinanceStoreKit.Now.AddDays(1));
        await _kit.InvoiceDeliveries().SaveAsync(stored, default);

        InvoiceDelivery found = (await _kit.InvoiceDeliveries().FindAsync(Big(closing).Id, default))!;
        found.Status.Should().Be(InvoiceDeliveryStatus.Queued);
        found.Attempts.Should().Be(0);
        found.RequestedBy.Should().Be(other);
        found.RequestedAt.Should().Be(FinanceStoreKit.Now.AddDays(1));
        found.SentAt.Should().Be(FinanceStoreKit.Now.AddMinutes(2));
        found.LastAttemptAt.Should().Be(FinanceStoreKit.Now.AddMinutes(2));
        found.FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task Save_FromAnotherCondominium_ChangesNothing()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        (await _kit.InvoiceDeliveries().AddAsync([Queued(Big(closing))], default)).IsSuccess.Should().BeTrue();
        InvoiceDelivery stored = (await _kit.InvoiceDeliveries().FindAsync(Big(closing).Id, default))!;
        stored.MarkSent(FinanceStoreKit.Now.AddMinutes(2));

        await _kit.InvoiceDeliveries(Guid.CreateVersion7()).SaveAsync(stored, default);

        (await _kit.InvoiceDeliveries().FindAsync(Big(closing).Id, default))!.Status.Should().Be(InvoiceDeliveryStatus.Queued);
    }

    [Fact]
    public async Task ReopeningTheMonth_TakesTheDeliveriesOfItsChargesAway()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        (await _kit.InvoiceDeliveries().AddAsync([Queued(Big(closing)), Queued(Small(closing))], default)).IsSuccess.Should().BeTrue();

        MonthClosing stored = (await _kit.Closings().FindAsync(October, default))!;
        (await _kit.Closings().RemoveAsync(stored, default)).IsSuccess.Should().BeTrue();

        (await _kit.InvoiceDeliveries().ListAsync([Big(closing).Id, Small(closing).Id], default)).Should().BeEmpty();
    }
}
