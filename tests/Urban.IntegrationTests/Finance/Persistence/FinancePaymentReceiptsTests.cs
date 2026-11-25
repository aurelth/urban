using FluentAssertions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.IntegrationTests.Finance.Persistence;

/// <summary>
/// O número do recibo no PostgreSQL: cada pagamento ganha o próximo da sequência do condomínio, sem repetir e sem buracos, mesmo
/// com gravações ao mesmo tempo em cobranças diferentes.
/// </summary>
[Collection(FinancePostgresCollection.Name)]
public class FinancePaymentReceiptsTests(FinancePostgresFixture postgres)
{
    private readonly FinanceStoreKit _kit = new(postgres);

    private static YearMonth October => FinanceStoreKit.M(2026, 10);

    private static UnitCharge Big(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).First();

    private static UnitCharge Small(MonthClosing closing) => closing.Charges.OrderBy(charge => charge.Position).Last();

    private async Task<Payment> StoreAsync(FinanceStoreKit kit, UnitCharge charge, decimal amount)
    {
        Payment payment = FinanceStoreKit.NewPayment(charge, October, amount);
        (await kit.Payments().AddAsync(payment, charge.Amount, default)).IsSuccess.Should().BeTrue();

        return payment;
    }

    [Fact]
    public async Task Add_GivesThePaymentsOfACondominiumTheNumbers1_2_3_AcrossCharges()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);

        Payment one = await StoreAsync(_kit, Big(closing), 100m);
        Payment two = await StoreAsync(_kit, Small(closing), 100m);
        Payment three = await StoreAsync(_kit, Big(closing), 100m);

        new[] { one, two, three }.Select(payment => payment.ReceiptNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Add_TheNumberIsKept_WhenThePaymentIsReadBack()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        await StoreAsync(_kit, Big(closing), 100m);
        Payment second = await StoreAsync(_kit, Big(closing), 100m);

        Payment? found = await _kit.Payments().FindAsync(second.Id, default);

        found!.ReceiptNumber.Should().Be(2);
        (await _kit.Payments().ListForChargeAsync(Big(closing).Id, default)).Select(payment => payment.ReceiptNumber).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task Add_EachCondominiumHasItsOwnSequence()
    {
        FinanceStoreKit other = new(postgres);
        MonthClosing mine = await _kit.StoreClosingAsync(October);
        MonthClosing theirs = await other.StoreClosingAsync(October);

        Payment first = await StoreAsync(_kit, Big(mine), 100m);
        Payment otherFirst = await StoreAsync(other, Big(theirs), 100m);
        Payment second = await StoreAsync(_kit, Big(mine), 100m);
        Payment otherSecond = await StoreAsync(other, Big(theirs), 100m);

        (first.ReceiptNumber, second.ReceiptNumber).Should().Be((1, 2));
        (otherFirst.ReceiptNumber, otherSecond.ReceiptNumber).Should().Be((1, 2));
    }

    [Fact]
    public async Task Add_ARefusedPayment_SpendsNoNumber()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        await StoreAsync(_kit, charge, 500m);

        Result refused = await _kit.Payments().AddAsync(FinanceStoreKit.NewPayment(charge, October, 200m), charge.Amount, default);
        Payment next = await StoreAsync(_kit, charge, 100m);

        refused.Error.Code.Should().Be(PaymentErrors.ExceedsBalance(100m).Code);
        next.ReceiptNumber.Should().Be(2);
    }

    [Fact]
    public async Task Add_SimultaneousPaymentsOnDifferentCharges_GetDistinctConsecutiveNumbers()
    {
        MonthClosing closing = await _kit.StoreClosingAsync(October);
        List<Payment> payments =
        [
            .. Enumerable.Range(0, 8).Select(index => FinanceStoreKit.NewPayment(index % 2 == 0 ? Big(closing) : Small(closing), October, 10m)),
        ];

        Result[] results = await Task.WhenAll(payments.Select(payment =>
            _kit.Payments().AddAsync(payment, payment.ChargeId == Big(closing).Id ? Big(closing).Amount : Small(closing).Amount, default)));

        results.Should().OnlyContain(result => result.IsSuccess);
        payments.Select(payment => payment.ReceiptNumber).Order().Should().Equal(Enumerable.Range(1, 8));
    }

    [Fact]
    public async Task Reverse_KeepsTheReceiptNumber()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        Payment stored = await StoreAsync(_kit, charge, 100m);
        Payment loaded = (await _kit.Payments().FindAsync(stored.Id, default))!;
        loaded.Reverse("Unidade errada", Guid.CreateVersion7(), FinanceStoreKit.Now);
        (await _kit.Payments().ReverseAsync(loaded, default)).IsSuccess.Should().BeTrue();

        Payment? found = await _kit.Payments().FindAsync(stored.Id, default);

        found!.IsReversed.Should().BeTrue();
        found.ReceiptNumber.Should().Be(1);
    }

    [Fact]
    public async Task AfterAReversal_TheNextPaymentStillGetsANewNumber()
    {
        UnitCharge charge = Big(await _kit.StoreClosingAsync(October));
        Payment stored = await StoreAsync(_kit, charge, 600m);
        Payment loaded = (await _kit.Payments().FindAsync(stored.Id, default))!;
        loaded.Reverse("Unidade errada", Guid.CreateVersion7(), FinanceStoreKit.Now);
        await _kit.Payments().ReverseAsync(loaded, default);

        Payment next = await StoreAsync(_kit, charge, 600m);

        next.ReceiptNumber.Should().Be(2);
    }
}
