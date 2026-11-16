using FluentAssertions;
using Urban.Finance.Application.Payments;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;

namespace Urban.UnitTests.Finance.Application;

/// <summary>Registrar, estornar e consultar pagamentos. Hoje é 7/11/2026; outubro está fechado com duas cobranças, 600 e 400, vencendo em 10/11.</summary>
public class PaymentHandlersTests
{
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 7);
    private static readonly DateOnly Received = new(2026, 11, 5);

    private readonly FinanceTestKit _kit = new();
    private readonly MonthClosing _closing;
    private readonly UnitCharge _first;
    private readonly UnitCharge _second;

    public PaymentHandlersTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        Expense expense = _kit.NewFixed(group, M(2026, 10), 1000m);
        _kit.NewEntry(expense, M(2026, 10));
        _closing = _kit.CloseMonth(M(2026, 10));
        _first = _closing.Charges.OrderBy(charge => charge.Position).First();
        _second = _closing.Charges.OrderBy(charge => charge.Position).Last();
    }

    private static YearMonth M(int year, int month) => FinanceTestKit.M(year, month);

    private RegisterPaymentCommandHandler RegisterHandler() => new(_kit.Payments, _kit.Tenant, _kit.Clock);

    private ReversePaymentCommandHandler ReverseHandler() => new(_kit.Payments, _kit.Tenant, _kit.Clock);

    private ListMonthPaymentsQueryHandler ListMonthHandler() => new(_kit.Payments, _kit.Tenant, _kit.Clock);

    private ListChargePaymentsQueryHandler ListChargeHandler() => new(_kit.Payments, _kit.Tenant, _kit.Clock);

    private Payment Paid(UnitCharge charge, decimal amount, DateOnly? receivedOn = null) =>
        _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, charge.Id, M(2026, 10), amount, receivedOn ?? Received, PaymentMethod.Pix, null, Manager, FinanceTestKit.Now, Today).Value);

    private static RegisterPaymentCommand Register(UnitCharge charge, decimal amount, DateOnly? receivedOn = null) =>
        new(charge.Id, amount, receivedOn ?? Received, PaymentMethod.Pix, null, Manager);

    // ---- Registrar ----

    [Fact]
    public async Task Register_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<PaymentView> result = await new RegisterPaymentCommandHandler(kit.Payments, kit.Tenant, kit.Clock).Handle(Register(_first, 100m), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Register_AChargeThatDoesNotExist_Fails()
    {
        Result<PaymentView> result = await RegisterHandler().Handle(new RegisterPaymentCommand(Guid.CreateVersion7(), 100m, Received, PaymentMethod.Pix, null, Manager), default);

        result.Error.Should().Be(PaymentErrors.ChargeNotFound);
        _kit.Payments.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_KeepsThePaymentWithWhoRegisteredIt_AndReturnsIt()
    {
        Result<PaymentView> result = await RegisterHandler().Handle(
            new RegisterPaymentCommand(_first.Id, 250.50m, Received, PaymentMethod.Transfer, "  Pagou pelo banco  ", Manager), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ChargeId.Should().Be(_first.Id);
        result.Value.Amount.Should().Be(250.50m);
        result.Value.ReceivedOn.Should().Be(Received);
        result.Value.Method.Should().Be(PaymentMethod.Transfer);
        result.Value.Note.Should().Be("Pagou pelo banco");
        result.Value.RegisteredAt.Should().Be(FinanceTestKit.Now);
        result.Value.ReversedAt.Should().BeNull();
        Payment stored = _kit.Payments.All.Should().ContainSingle().Subject;
        stored.Id.Should().Be(result.Value.Id);
        stored.RegisteredBy.Should().Be(Manager);
        stored.CondominiumId.Should().Be(_kit.CondominiumId);
    }

    [Fact]
    public async Task Register_SeveralPaymentsOnTheSameCharge_AreAllowedUpToTheChargeAmount()
    {
        (await RegisterHandler().Handle(Register(_first, 200m), default)).IsSuccess.Should().BeTrue();
        (await RegisterHandler().Handle(Register(_first, 400m), default)).IsSuccess.Should().BeTrue();

        _kit.Payments.All.Should().HaveCount(2);
    }

    [Fact]
    public async Task Register_MoreThanWhatIsStillOwed_IsRefusedWithTheBalance()
    {
        Paid(_first, 500m);

        Result<PaymentView> result = await RegisterHandler().Handle(Register(_first, 100.01m), default);

        result.Error.Code.Should().Be(PaymentErrors.ExceedsBalance(100m).Code);
        result.Error.Data!["balance"].Should().Be(100m);
        _kit.Payments.All.Should().ContainSingle();
    }

    [Fact]
    public async Task Register_APaymentThatWasReversed_NoLongerCountsAgainstTheBalance()
    {
        Payment wrong = Paid(_first, 600m);
        wrong.Reverse("Unidade errada", Manager, FinanceTestKit.Now);

        (await RegisterHandler().Handle(Register(_first, 600m), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Register_AnInvalidAmount_IsRefused_AndNothingIsKept()
    {
        (await RegisterHandler().Handle(Register(_first, 0m), default)).Error.Should().Be(PaymentErrors.AmountInvalid);
        _kit.Payments.All.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_ADateInTheFuture_IsRefused()
    {
        (await RegisterHandler().Handle(Register(_first, 100m, Today.AddDays(1)), default)).Error.Should().Be(PaymentErrors.DateInvalid);
        (await RegisterHandler().Handle(Register(_first, 100m, Today), default)).IsSuccess.Should().BeTrue();
    }

    // ---- Estornar ----

    [Fact]
    public async Task Reverse_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result result = await new ReversePaymentCommandHandler(kit.Payments, kit.Tenant, kit.Clock).Handle(new ReversePaymentCommand(Guid.CreateVersion7(), "Erro", Manager), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Reverse_APaymentThatDoesNotExist_Fails()
    {
        (await ReverseHandler().Handle(new ReversePaymentCommand(Guid.CreateVersion7(), "Erro", Manager), default)).Error.Should().Be(PaymentErrors.NotFound);
    }

    [Fact]
    public async Task Reverse_MarksThePaymentAndKeepsIt()
    {
        Payment payment = Paid(_first, 300m);

        Result result = await ReverseHandler().Handle(new ReversePaymentCommand(payment.Id, "Lançado na unidade errada", Manager), default);

        result.IsSuccess.Should().BeTrue();
        payment.IsReversed.Should().BeTrue();
        payment.ReversedBy.Should().Be(Manager);
        payment.ReversedAt.Should().Be(FinanceTestKit.Now);
        payment.ReversalReason.Should().Be("Lançado na unidade errada");
        _kit.Payments.Reversed.Should().ContainSingle().Which.Should().BeSameAs(payment);
        _kit.Payments.All.Should().ContainSingle();
    }

    [Fact]
    public async Task Reverse_APaymentThatIsAlreadyReversed_Fails()
    {
        Payment payment = Paid(_first, 300m);
        payment.Reverse("Primeiro estorno", Manager, FinanceTestKit.Now);

        (await ReverseHandler().Handle(new ReversePaymentCommand(payment.Id, "Outro", Manager), default)).Error.Should().Be(PaymentErrors.AlreadyReversed);
        _kit.Payments.Reversed.Should().BeEmpty();
    }

    [Fact]
    public async Task Reverse_WithoutAReason_Fails_AndKeepsThePaymentCounting()
    {
        Payment payment = Paid(_first, 300m);

        (await ReverseHandler().Handle(new ReversePaymentCommand(payment.Id, "  ", Manager), default)).Error.Should().Be(PaymentErrors.ReasonInvalid);
        payment.IsReversed.Should().BeFalse();
        _kit.Payments.Reversed.Should().BeEmpty();
    }

    // ---- Lista do mês ----

    [Fact]
    public async Task ListMonth_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        (await new ListMonthPaymentsQueryHandler(kit.Payments, kit.Tenant, kit.Clock).Handle(new ListMonthPaymentsQuery("2026-10"), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task ListMonth_AnInvalidMonth_Fails()
    {
        (await ListMonthHandler().Handle(new ListMonthPaymentsQuery("10/2026"), default)).Error.Should().Be(MonthErrors.Invalid);
    }

    [Fact]
    public async Task ListMonth_AMonthThatIsNotClosed_Fails()
    {
        (await ListMonthHandler().Handle(new ListMonthPaymentsQuery("2026-09"), default)).Error.Should().Be(ClosingErrors.NotClosed);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    [InlineData(-1, 50)]
    public async Task ListMonth_AnInvalidPage_Fails(int page, int pageSize)
    {
        (await ListMonthHandler().Handle(new ListMonthPaymentsQuery("2026-10", null, page, pageSize), default)).Error.Should().Be(PaymentErrors.PageInvalid);
    }

    [Fact]
    public async Task ListMonth_ShowsEachChargeWithWhatWasPaidTheBalanceAndTheStatus()
    {
        Paid(_first, 600m, new DateOnly(2026, 11, 3));
        Paid(_second, 100m, new DateOnly(2026, 11, 4));
        Paid(_second, 50m, new DateOnly(2026, 11, 6));
        Payment wrong = Paid(_second, 30m, new DateOnly(2026, 11, 7));
        wrong.Reverse("Erro", Manager, FinanceTestKit.Now);

        Result<MonthPaymentsView> result = await ListMonthHandler().Handle(new ListMonthPaymentsQuery("2026-10"), default);

        result.IsSuccess.Should().BeTrue();
        MonthPaymentsView view = result.Value;
        view.Month.Should().Be("2026-10");
        view.DueDate.Should().Be(_closing.DueDate);
        view.Charged.Should().Be(1000m);
        view.Received.Should().Be(750m);
        view.Total.Should().Be(2);
        view.Page.Should().Be(1);
        view.PageSize.Should().Be(50);
        view.Items.Should().HaveCount(2);
        ChargeSummaryView paid = view.Items[0];
        paid.ChargeId.Should().Be(_first.Id);
        paid.Amount.Should().Be(600m);
        paid.Paid.Should().Be(600m);
        paid.Balance.Should().Be(0m);
        paid.Status.Should().Be(ChargeStatus.Paid);
        paid.LastReceivedOn.Should().Be(new DateOnly(2026, 11, 3));
        ChargeSummaryView partial = view.Items[1];
        partial.Paid.Should().Be(150m);
        partial.Balance.Should().Be(250m);
        partial.Status.Should().Be(ChargeStatus.Partial);
        partial.LastReceivedOn.Should().Be(new DateOnly(2026, 11, 6));
    }

    [Fact]
    public async Task ListMonth_AChargeNobodyPaidIsOpenUntilTheDueDate_ThenOverdue()
    {
        (await ListMonthHandler().Handle(new ListMonthPaymentsQuery("2026-10"), default)).Value.Items.Should().OnlyContain(item => item.Status == ChargeStatus.Open && item.LastReceivedOn == null);

        FinanceTestKit late = new();
        ExpenseGroup group = late.NewGroup();
        late.NewEntry(late.NewFixed(group, M(2026, 9), 1000m), M(2026, 9));
        late.CloseMonth(M(2026, 9), new DateOnly(2026, 11, 6));

        Result<MonthPaymentsView> result = await new ListMonthPaymentsQueryHandler(late.Payments, late.Tenant, late.Clock).Handle(new ListMonthPaymentsQuery("2026-09"), default);

        result.Value.Items.Should().OnlyContain(item => item.Status == ChargeStatus.Overdue);
    }

    [Fact]
    public async Task ListMonth_FilteringByStatus_KeepsTheTotalsOfTheWholeMonth()
    {
        Paid(_first, 600m);

        Result<MonthPaymentsView> result = await ListMonthHandler().Handle(new ListMonthPaymentsQuery("2026-10", ChargeStatus.Open), default);

        result.Value.Items.Should().ContainSingle().Which.ChargeId.Should().Be(_second.Id);
        result.Value.Total.Should().Be(1);
        result.Value.Charged.Should().Be(1000m);
        result.Value.Received.Should().Be(600m);
    }

    [Fact]
    public async Task ListMonth_Paginates()
    {
        Result<MonthPaymentsView> result = await ListMonthHandler().Handle(new ListMonthPaymentsQuery("2026-10", null, 2, 1), default);

        result.Value.Total.Should().Be(2);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(1);
        result.Value.Items.Should().ContainSingle().Which.ChargeId.Should().Be(_second.Id);
    }

    // ---- Pagamentos de uma cobrança ----

    [Fact]
    public async Task ListCharge_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        (await new ListChargePaymentsQueryHandler(kit.Payments, kit.Tenant, kit.Clock).Handle(new ListChargePaymentsQuery(_first.Id), default)).Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task ListCharge_AChargeThatDoesNotExist_Fails()
    {
        (await ListChargeHandler().Handle(new ListChargePaymentsQuery(Guid.CreateVersion7()), default)).Error.Should().Be(PaymentErrors.ChargeNotFound);
    }

    [Fact]
    public async Task ListCharge_ShowsTheNewestFirst_ReversedOnesIncluded_ButOnlyTheOthersCount()
    {
        Payment older = Paid(_first, 200m, new DateOnly(2026, 11, 3));
        Payment reversed = Paid(_first, 100m, new DateOnly(2026, 11, 4));
        reversed.Reverse("Erro de digitação", Manager, FinanceTestKit.Now);
        Payment newer = Paid(_first, 50m, new DateOnly(2026, 11, 5));

        Result<ChargePaymentsView> result = await ListChargeHandler().Handle(new ListChargePaymentsQuery(_first.Id), default);

        result.IsSuccess.Should().BeTrue();
        ChargePaymentsView view = result.Value;
        view.ChargeId.Should().Be(_first.Id);
        view.Month.Should().Be("2026-10");
        view.DueDate.Should().Be(_closing.DueDate);
        view.Amount.Should().Be(600m);
        view.Paid.Should().Be(250m);
        view.Balance.Should().Be(350m);
        view.Status.Should().Be(ChargeStatus.Partial);
        view.Payments.Select(payment => payment.Id).Should().Equal(newer.Id, reversed.Id, older.Id);
        view.Payments[1].ReversedAt.Should().Be(FinanceTestKit.Now);
        view.Payments[1].ReversalReason.Should().Be("Erro de digitação");
    }

    [Fact]
    public async Task ListCharge_WithNoPayments_IsEmptyAndOpen()
    {
        Result<ChargePaymentsView> result = await ListChargeHandler().Handle(new ListChargePaymentsQuery(_second.Id), default);

        result.Value.Payments.Should().BeEmpty();
        result.Value.Paid.Should().Be(0m);
        result.Value.Balance.Should().Be(400m);
        result.Value.Status.Should().Be(ChargeStatus.Open);
    }
}
