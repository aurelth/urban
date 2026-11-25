using FluentAssertions;
using Urban.Finance.Application.Payments;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

/// <summary>Todo pagamento registrado já nasce com o número do recibo, na sequência do condomínio. Hoje é 7/11/2026; outubro está fechado com 600 e 400.</summary>
public class RegisterPaymentReceiptTests
{
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateOnly Received = new(2026, 11, 5);

    private readonly FinanceTestKit _kit = new();
    private readonly UnitCharge _first;
    private readonly UnitCharge _second;

    public RegisterPaymentReceiptTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        Expense expense = _kit.NewFixed(group, FinanceTestKit.M(2026, 10), 1000m);
        _kit.NewEntry(expense, FinanceTestKit.M(2026, 10));
        MonthClosing closing = _kit.CloseMonth(FinanceTestKit.M(2026, 10));
        _first = closing.Charges.OrderBy(charge => charge.Position).First();
        _second = closing.Charges.OrderBy(charge => charge.Position).Last();
    }

    private Task<Result<PaymentView>> RegisterAsync(UnitCharge charge, decimal amount) =>
        new RegisterPaymentCommandHandler(_kit.Payments, _kit.Tenant, _kit.Clock)
            .Handle(new RegisterPaymentCommand(charge.Id, amount, Received, PaymentMethod.Pix, null, Manager), default);

    [Fact]
    public async Task Register_GivesEachPaymentTheNextReceiptNumber_AcrossCharges()
    {
        Result<PaymentView> one = await RegisterAsync(_first, 100m);
        Result<PaymentView> two = await RegisterAsync(_second, 100m);
        Result<PaymentView> three = await RegisterAsync(_first, 100m);

        one.Value.ReceiptNumber.Should().Be(1);
        two.Value.ReceiptNumber.Should().Be(2);
        three.Value.ReceiptNumber.Should().Be(3);
    }

    [Fact]
    public async Task Register_TheStoredPaymentCarriesTheNumberToo()
    {
        Result<PaymentView> result = await RegisterAsync(_first, 100m);

        _kit.Payments.All.Single().ReceiptNumber.Should().Be(result.Value.ReceiptNumber).And.Be(1);
    }

    [Fact]
    public async Task Register_ARefusedPayment_SpendsNoNumber()
    {
        await RegisterAsync(_first, 500m);

        Result<PaymentView> tooMuch = await RegisterAsync(_first, 200m);
        Result<PaymentView> next = await RegisterAsync(_first, 100m);

        tooMuch.IsFailure.Should().BeTrue();
        next.Value.ReceiptNumber.Should().Be(2);
    }

    [Fact]
    public async Task ListByCharge_ShowsTheReceiptNumberOfEachPayment_AlsoOfTheReversedOnes()
    {
        Result<PaymentView> one = await RegisterAsync(_first, 100m);
        await RegisterAsync(_first, 50m);
        Payment stored = _kit.Payments.All.First(payment => payment.Id == one.Value.Id);
        stored.Reverse("Unidade errada", Manager, FinanceTestKit.Now);

        Result<ChargePaymentsView> list = await new ListChargePaymentsQueryHandler(_kit.Payments, _kit.Tenant, _kit.Clock)
            .Handle(new ListChargePaymentsQuery(_first.Id), default);

        list.Value.Payments.Select(payment => payment.ReceiptNumber).Should().BeEquivalentTo([1, 2]);
    }
}
