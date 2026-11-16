using FluentAssertions;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.Finance.Infrastructure.Persistence;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

[Collection(FinancePostgresCollection.Name)]
public class FinancePaymentsTests(FinancePostgresFixture postgres)
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    private readonly FinanceStoreKit _kit = new(postgres);

    private static YearMonth October => FinanceStoreKit.M(2026, 10);

    private static UnitCharge Big(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).First();

    private static UnitCharge Small(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).Last();

    private async Task<Payment> StorePaymentAsync(UnitCharge charge, decimal amount, DateOnly? receivedOn = null, DateTimeOffset? registeredAt = null)
    {
        Payment payment = FinanceStoreKit.NewPayment(charge, October, amount, receivedOn, registeredAt);
        (await _kit.Payments().AddAsync(payment, charge.Amount, default)).IsSuccess.Should().BeTrue();

        return payment;
    }

    // ---- Cobrança e gravação ----

    [Fact]
    public async Task FindCharge_ReturnsTheChargeWithItsMonthDueDateAndAmount()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        UnitCharge charge = Big(closing);

        ChargeRef? found = await _kit.Payments().FindChargeAsync(charge.Id, default);

        found.Should().NotBeNull();
        found!.ChargeId.Should().Be(charge.Id);
        found.ClosingId.Should().Be(closing.Id);
        found.Month.Should().Be(October);
        found.DueDate.Should().Be(closing.DueDate);
        found.Amount.Should().Be(600m);
    }

    [Fact]
    public async Task FindCharge_OfAnotherCondominium_IsNotFound()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);

        (await _kit.Payments(Guid.CreateVersion7()).FindChargeAsync(Big(closing).Id, default)).Should().BeNull();
        (await _kit.Payments().FindChargeAsync(Guid.CreateVersion7(), default)).Should().BeNull();
    }

    [Fact]
    public async Task Add_ThenFind_KeepsEveryField()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        Payment payment = Payment.Register(
            _kit.CondominiumId, Big(closing).Id, October, 123.45m, new DateOnly(2026, 11, 4), PaymentMethod.Boleto, "Pagou no balcão", Guid.CreateVersion7(), FinanceStoreKit.Now, Today).Value;
        (await _kit.Payments().AddAsync(payment, 600m, default)).IsSuccess.Should().BeTrue();

        Payment? found = await _kit.Payments().FindAsync(payment.Id, default);

        found.Should().NotBeNull();
        found!.ChargeId.Should().Be(Big(closing).Id);
        found.CondominiumId.Should().Be(_kit.CondominiumId);
        found.Amount.Should().Be(123.45m);
        found.ReceivedOn.Should().Be(new DateOnly(2026, 11, 4));
        found.Method.Should().Be(PaymentMethod.Boleto);
        found.Note.Should().Be("Pagou no balcão");
        found.RegisteredBy.Should().Be(payment.RegisteredBy);
        found.RegisteredAt.Should().Be(FinanceStoreKit.Now);
        found.IsReversed.Should().BeFalse();
    }

    [Fact]
    public async Task Find_APaymentOfAnotherCondominium_IsNotFound()
    {
        Payment payment = await StorePaymentAsync(Big(await _kit.StoreClosingAsync(October)), 100m);

        (await _kit.Payments(Guid.CreateVersion7()).FindAsync(payment.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Add_MoreThanTheBalance_IsRefused_AndNothingIsKept()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        await StorePaymentAsync(charge, 500m);

        Result result = await _kit.Payments().AddAsync(FinanceStoreKit.NewPayment(charge, October, 100.01m), charge.Amount, default);

        result.Error.Code.Should().Be(PaymentErrors.ExceedsBalance(100m).Code);
        result.Error.Data!["balance"].Should().Be(100m);
        (await _kit.Payments().ListForChargeAsync(charge.Id, default)).Should().ContainSingle();
    }

    [Fact]
    public async Task Add_UpToTheBalanceExactly_IsAccepted()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        await StorePaymentAsync(charge, 500m);

        (await _kit.Payments().AddAsync(FinanceStoreKit.NewPayment(charge, October, 100m), charge.Amount, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Add_TwoAtTheSameTime_OnlyOneFitsTheBalance()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));

        Result[] results = await Task.WhenAll(
            _kit.Payments().AddAsync(FinanceStoreKit.NewPayment(charge, October, 400m), charge.Amount, default),
            _kit.Payments().AddAsync(FinanceStoreKit.NewPayment(charge, October, 400m), charge.Amount, default));

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Single(result => result.IsFailure).Error.Code.Should().Be(PaymentErrors.ExceedsBalance(200m).Code);
        (await _kit.Payments().ListForChargeAsync(charge.Id, default)).Should().ContainSingle();
    }

    [Fact]
    public async Task Add_AfterAReversal_TheReversedAmountIsFreeAgain()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        Payment wrong = await StorePaymentAsync(charge, 600m);
        Payment loaded = (await _kit.Payments().FindAsync(wrong.Id, default))!;
        loaded.Reverse("Unidade errada", Guid.CreateVersion7(), FinanceStoreKit.Now);
        (await _kit.Payments().ReverseAsync(loaded, default)).IsSuccess.Should().BeTrue();

        (await _kit.Payments().AddAsync(FinanceStoreKit.NewPayment(charge, October, 600m), charge.Amount, default)).IsSuccess.Should().BeTrue();
    }

    // ---- Estorno ----

    [Fact]
    public async Task Reverse_KeepsThePaymentMarkedWithWhoWhenAndWhy()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        Payment payment = await StorePaymentAsync(charge, 300m);
        Payment loaded = (await _kit.Payments().FindAsync(payment.Id, default))!;
        Guid reverser = Guid.CreateVersion7();
        DateTimeOffset at = FinanceStoreKit.Now.AddHours(2);
        loaded.Reverse("Lançado na unidade errada", reverser, at);

        Result result = await _kit.Payments().ReverseAsync(loaded, default);

        result.IsSuccess.Should().BeTrue();
        Payment stored = (await _kit.Payments().FindAsync(payment.Id, default))!;
        stored.IsReversed.Should().BeTrue();
        stored.ReversedAt.Should().Be(at);
        stored.ReversedBy.Should().Be(reverser);
        stored.ReversalReason.Should().Be("Lançado na unidade errada");
        stored.Amount.Should().Be(300m);
    }

    [Fact]
    public async Task Reverse_TwoAtTheSameTime_OnlyTheFirstWins()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        Payment payment = await StorePaymentAsync(charge, 300m);
        Payment first = (await _kit.Payments().FindAsync(payment.Id, default))!;
        Payment second = (await _kit.Payments().FindAsync(payment.Id, default))!;
        first.Reverse("Primeiro", Guid.CreateVersion7(), FinanceStoreKit.Now).IsSuccess.Should().BeTrue();
        second.Reverse("Segundo", Guid.CreateVersion7(), FinanceStoreKit.Now.AddMinutes(1)).IsSuccess.Should().BeTrue();

        Result winner = await _kit.Payments().ReverseAsync(first, default);
        Result loser = await _kit.Payments().ReverseAsync(second, default);

        winner.IsSuccess.Should().BeTrue();
        loser.Error.Should().Be(PaymentErrors.AlreadyReversed);
        (await _kit.Payments().FindAsync(payment.Id, default))!.ReversalReason.Should().Be("Primeiro");
    }

    [Fact]
    public async Task Reverse_APaymentOfAnotherCondominium_ChangesNothing()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        Payment payment = await StorePaymentAsync(charge, 300m);
        Payment loaded = (await _kit.Payments().FindAsync(payment.Id, default))!;
        loaded.Reverse("Erro", Guid.CreateVersion7(), FinanceStoreKit.Now);

        Result result = await _kit.Payments(Guid.CreateVersion7()).ReverseAsync(loaded, default);

        result.IsFailure.Should().BeTrue();
        (await _kit.Payments().FindAsync(payment.Id, default))!.IsReversed.Should().BeFalse();
    }

    // ---- Lista de uma cobrança ----

    [Fact]
    public async Task ListForCharge_ReturnsTheNewestFirst_ReversedIncluded_AndOnlyThatCharge()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        Payment older = await StorePaymentAsync(Big(closing), 100m, registeredAt: FinanceStoreKit.Now.AddMinutes(1));
        Payment newer = await StorePaymentAsync(Big(closing), 50m, registeredAt: FinanceStoreKit.Now.AddMinutes(5));
        await StorePaymentAsync(Small(closing), 10m);
        Payment loaded = (await _kit.Payments().FindAsync(older.Id, default))!;
        loaded.Reverse("Erro", Guid.CreateVersion7(), FinanceStoreKit.Now);
        await _kit.Payments().ReverseAsync(loaded, default);

        IReadOnlyList<Payment> payments = await _kit.Payments().ListForChargeAsync(Big(closing).Id, default);

        payments.Select(payment => payment.Id).Should().Equal(newer.Id, older.Id);
        payments[1].IsReversed.Should().BeTrue();
    }

    [Fact]
    public async Task ListForCharge_OfAnotherCondominium_IsEmpty()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        await StorePaymentAsync(charge, 100m);

        (await _kit.Payments(Guid.CreateVersion7()).ListForChargeAsync(charge.Id, default)).Should().BeEmpty();
    }

    // ---- Existe pagamento no fechamento ----

    [Fact]
    public async Task AnyForClosing_IsFalseUntilThereIsAPayment_AndStaysTrueEvenIfItWasReversed()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        MonthClosing other = await _kit.StoreClosingAsync(FinanceStoreKit.M(2026, 9));
        (await _kit.Payments().AnyForClosingAsync(closing.Id, default)).Should().BeFalse();

        Payment payment = await StorePaymentAsync(Small(closing), 100m);
        Payment loaded = (await _kit.Payments().FindAsync(payment.Id, default))!;
        loaded.Reverse("Erro", Guid.CreateVersion7(), FinanceStoreKit.Now);
        await _kit.Payments().ReverseAsync(loaded, default);

        (await _kit.Payments().AnyForClosingAsync(closing.Id, default)).Should().BeTrue();
        (await _kit.Payments().AnyForClosingAsync(other.Id, default)).Should().BeFalse();
    }

    [Fact]
    public async Task RemovingAClosingThatHasPayments_IsRefused_AndKeepsEverything()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        await StorePaymentAsync(Big(closing), 100m);
        FinanceMonthClosings closings = _kit.Closings();
        MonthClosing loaded = (await closings.FindAsync(October, default))!;

        Result result = await closings.RemoveAsync(loaded, default);

        result.Error.Should().Be(ClosingErrors.HasPayments);
        (await _kit.Closings().FindAsync(October, default)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemovingAClosingWithoutPayments_Works()
    {
        await _kit.StoreClosingAsync(October);
        FinanceMonthClosings closings = _kit.Closings();
        MonthClosing loaded = (await closings.FindAsync(October, default))!;

        (await closings.RemoveAsync(loaded, default)).IsSuccess.Should().BeTrue();
        (await _kit.Closings().FindAsync(October, default)).Should().BeNull();
    }

    // ---- Lista do mês ----

    [Fact]
    public async Task ListMonth_AMonthThatIsNotClosed_IsNull()
    {
        (await _kit.Payments().ListMonthAsync(October, null, Today, 1, 50, default)).Should().BeNull();
    }

    [Fact]
    public async Task ListMonth_OfAnotherCondominium_IsNull()
    {
        await _kit.StoreClosingAsync(October);

        (await _kit.Payments(Guid.CreateVersion7()).ListMonthAsync(October, null, Today, 1, 50, default)).Should().BeNull();
    }

    [Fact]
    public async Task ListMonth_ReturnsOneRowPerChargeInTheClosingOrder_WithWhatWasPaid()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        await StorePaymentAsync(Big(closing), 200m, new DateOnly(2026, 11, 3));
        await StorePaymentAsync(Big(closing), 100m, new DateOnly(2026, 11, 6));
        Payment wrong = await StorePaymentAsync(Big(closing), 50m, new DateOnly(2026, 11, 7));
        Payment loaded = (await _kit.Payments().FindAsync(wrong.Id, default))!;
        loaded.Reverse("Erro", Guid.CreateVersion7(), FinanceStoreKit.Now);
        await _kit.Payments().ReverseAsync(loaded, default);

        MonthPaymentsPage? page = await _kit.Payments().ListMonthAsync(October, null, Today, 1, 50, default);

        page.Should().NotBeNull();
        page!.DueDate.Should().Be(closing.DueDate);
        page.Charged.Should().Be(1000m);
        page.Received.Should().Be(300m);
        page.Total.Should().Be(2);
        page.Items.Select(row => (row.ChargeId, row.UnitId, row.Block, row.Number, row.Amount, row.Paid, row.LastReceivedOn)).Should().Equal(
            (Big(closing).Id, Big(closing).UnitId, Big(closing).Block, Big(closing).Number, 600m, 300m, (DateOnly?)new DateOnly(2026, 11, 6)),
            (Small(closing).Id, Small(closing).UnitId, Small(closing).Block, Small(closing).Number, 400m, 0m, (DateOnly?)null));
    }

    [Fact]
    public async Task ListMonth_PaymentsOfAnotherMonthDoNotMix()
    {
        MonthClosing october = await _kit.StoreClosingAsync(October);
        MonthClosing september = await _kit.StoreClosingAsync(FinanceStoreKit.M(2026, 9));
        await StorePaymentAsync(Big(september), 100m);

        MonthPaymentsPage page = (await _kit.Payments().ListMonthAsync(October, null, Today, 1, 50, default))!;

        page.Received.Should().Be(0m);
        page.Items.Should().OnlyContain(row => row.Paid == 0m);
        october.Charges.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListMonth_FiltersByTheCalculatedStatus_ButTheTotalsStayForTheWholeMonth()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October, new DateOnly(2026, 11, 10));
        await StorePaymentAsync(Big(closing), 600m);

        async Task<MonthPaymentsPage> List(ChargeStatus status, DateOnly today) =>
            (await _kit.Payments().ListMonthAsync(October, status, today, 1, 50, default))!;

        (await List(ChargeStatus.Paid, Today)).Items.Select(row => row.ChargeId).Should().Equal(Big(closing).Id);
        (await List(ChargeStatus.Open, Today)).Items.Select(row => row.ChargeId).Should().Equal(Small(closing).Id);
        (await List(ChargeStatus.Partial, Today)).Items.Should().BeEmpty();
        (await List(ChargeStatus.Overdue, Today)).Items.Should().BeEmpty();
        MonthPaymentsPage afterDue = await List(ChargeStatus.Overdue, new DateOnly(2026, 11, 11));
        afterDue.Items.Select(row => row.ChargeId).Should().Equal(Small(closing).Id);
        afterDue.Total.Should().Be(1);
        afterDue.Charged.Should().Be(1000m);
        afterDue.Received.Should().Be(600m);
    }

    [Fact]
    public async Task ListMonth_APartiallyPaidChargeIsPartialUntilTheDueDate_ThenOverdue()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October, new DateOnly(2026, 11, 10));
        await StorePaymentAsync(Small(closing), 100m);

        (await _kit.Payments().ListMonthAsync(October, ChargeStatus.Partial, new DateOnly(2026, 11, 10), 1, 50, default))!
            .Items.Select(row => row.ChargeId).Should().Equal(Small(closing).Id);
        (await _kit.Payments().ListMonthAsync(October, ChargeStatus.Partial, new DateOnly(2026, 11, 11), 1, 50, default))!.Items.Should().BeEmpty();
        (await _kit.Payments().ListMonthAsync(October, ChargeStatus.Overdue, new DateOnly(2026, 11, 11), 1, 50, default))!.Total.Should().Be(2);
    }

    [Fact]
    public async Task ListMonth_Paginates()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);

        MonthPaymentsPage first = (await _kit.Payments().ListMonthAsync(October, null, Today, 1, 1, default))!;
        MonthPaymentsPage second = (await _kit.Payments().ListMonthAsync(October, null, Today, 2, 1, default))!;
        MonthPaymentsPage third = (await _kit.Payments().ListMonthAsync(October, null, Today, 3, 1, default))!;

        first.Total.Should().Be(2);
        first.Items.Select(row => row.ChargeId).Should().Equal(Big(closing).Id);
        second.Items.Select(row => row.ChargeId).Should().Equal(Small(closing).Id);
        third.Items.Should().BeEmpty();
        third.Total.Should().Be(2);
    }
}
