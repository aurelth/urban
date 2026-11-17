using FluentAssertions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// O envio das faturas de um mês por e-mail. Hoje é 7/11/2026; outubro está fechado com duas cobranças, a da unidade 101 (600) e a do
/// bloco A, 102 (400). O responsável da 101 tem e-mail; o da 102 só entra nos testes que o pedem.
/// </summary>
public class InvoiceDeliveryHandlersTests
{
    private static readonly Guid Actor = Guid.CreateVersion7();

    private readonly FinanceTestKit _kit = new();
    private readonly UnitCharge _first;
    private readonly UnitCharge _second;

    public InvoiceDeliveryHandlersTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        Expense expense = _kit.NewFixed(group, FinanceTestKit.M(2026, 10), 1000m);
        _kit.NewEntry(expense, FinanceTestKit.M(2026, 10));
        MonthClosing closing = _kit.CloseMonth(FinanceTestKit.M(2026, 10), new DateOnly(2026, 11, 10));
        _first = closing.Charges.Single(charge => charge.Number == "101");
        _second = closing.Charges.Single(charge => charge.Number == "102");
        _kit.Parties.Responsibles[_first.UnitId] = new InvoiceResponsible("Maria Souza", "maria@exemplo.com");
    }

    private SendMonthInvoicesCommandHandler SendHandler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new SendMonthInvoicesCommandHandler(used.Deliveries, used.Parties, used.Dispatcher, used.Tenant, used.Clock);
    }

    private ResendInvoiceCommandHandler ResendHandler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new ResendInvoiceCommandHandler(used.Deliveries, used.Parties, used.Dispatcher, used.Tenant, used.Clock);
    }

    private ListInvoiceDeliveriesQueryHandler ListHandler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new ListInvoiceDeliveriesQueryHandler(used.Deliveries, used.Parties, used.Tenant, used.Clock);
    }

    private void GiveTheSecondUnitAnEmail() => _kit.Parties.Responsibles[_second.UnitId] = new InvoiceResponsible("João Lima", "joao@exemplo.com");

    // ---- Enviar o mês ----

    [Fact]
    public async Task SendMonth_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<SendMonthInvoicesResult> result = await SendHandler(kit).Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Dispatcher.Enqueued.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("outubro")]
    [InlineData("2026-13")]
    public async Task SendMonth_WithAnInvalidMonth_Fails(string? month)
    {
        Result<SendMonthInvoicesResult> result = await SendHandler().Handle(new SendMonthInvoicesCommand(month, Actor), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(YearMonth.Parse(month).Error);
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task SendMonth_OfAMonthThatIsNotClosed_Fails()
    {
        Result<SendMonthInvoicesResult> result = await SendHandler().Handle(new SendMonthInvoicesCommand("2026-09", Actor), default);

        result.Error.Should().Be(ClosingErrors.NotClosed);
        _kit.Deliveries.Added.Should().BeEmpty();
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task SendMonth_QueuesEveryUnitWithAnEmail_CarryingTheCondominiumAndTheCharge()
    {
        GiveTheSecondUnitAnEmail();

        Result<SendMonthInvoicesResult> result = await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        result.Value.Should().Be(new SendMonthInvoicesResult(2, 0, 0, 0));
        _kit.Deliveries.Added.Should().HaveCount(2);
        _kit.Deliveries.Added.Should().OnlyContain(delivery =>
            delivery.Status == InvoiceDeliveryStatus.Queued && delivery.RequestedBy == Actor && delivery.RequestedAt == FinanceTestKit.Now && delivery.CondominiumId == _kit.CondominiumId);
        _kit.Dispatcher.Enqueued.Should().BeEquivalentTo([(_kit.CondominiumId, _first.Id), (_kit.CondominiumId, _second.Id)]);
    }

    [Fact]
    public async Task SendMonth_AsksForTheResponsiblesOfAllTheUnitsAtOnce_OnTheBrasiliaDate()
    {
        await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        _kit.Parties.AskedAbout.Should().BeEquivalentTo([_first.UnitId, _second.UnitId]);
        _kit.Parties.AskedOn.Should().Be(new DateOnly(2026, 11, 7));
        _kit.Deliveries.ListCalls.Should().Be(1);
    }

    [Fact]
    public async Task SendMonth_LeavesOutTheUnitWithoutAnEmailAndTheOneWithoutAResponsible()
    {
        _kit.Parties.Responsibles[_second.UnitId] = new InvoiceResponsible("João Lima", null);

        Result<SendMonthInvoicesResult> result = await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        result.Value.Should().Be(new SendMonthInvoicesResult(1, 0, 0, 1));
        _kit.Dispatcher.Enqueued.Should().ContainSingle().Which.ChargeId.Should().Be(_first.Id);

        _kit.Parties.Responsibles.Remove(_second.UnitId);
        (await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default)).Value.WithoutRecipient.Should().Be(1);
    }

    [Fact]
    public async Task SendMonth_DoesNotRepeatWhatWasAlreadySentOrIsInTheQueue()
    {
        GiveTheSecondUnitAnEmail();
        _kit.Deliveries.Existing(_first, InvoiceDeliveryStatus.Sent, FinanceTestKit.Now.AddDays(-1));
        _kit.Deliveries.Existing(_second, InvoiceDeliveryStatus.Queued, FinanceTestKit.Now.AddMinutes(-5));

        Result<SendMonthInvoicesResult> result = await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        result.Value.Should().Be(new SendMonthInvoicesResult(0, 1, 1, 0));
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
        _kit.Deliveries.Added.Should().BeEmpty();
        _kit.Deliveries.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task SendMonth_QueuesAgainTheFailedAndTheLostOnes_KeepingTheLastSend()
    {
        GiveTheSecondUnitAnEmail();
        InvoiceDelivery failed = _kit.Deliveries.Existing(_first, InvoiceDeliveryStatus.Failed, FinanceTestKit.Now.AddHours(-2));
        InvoiceDelivery lost = _kit.Deliveries.Existing(_second, InvoiceDeliveryStatus.Queued, FinanceTestKit.Now.AddHours(-7));

        Result<SendMonthInvoicesResult> result = await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        result.Value.Should().Be(new SendMonthInvoicesResult(2, 0, 0, 0));
        failed.Status.Should().Be(InvoiceDeliveryStatus.Queued);
        failed.Attempts.Should().Be(0);
        failed.FailureReason.Should().BeNull();
        failed.RequestedBy.Should().Be(Actor);
        lost.RequestedAt.Should().Be(FinanceTestKit.Now);
        _kit.Deliveries.Saved.Should().BeEquivalentTo([failed, lost]);
        _kit.Deliveries.Added.Should().BeEmpty();
        _kit.Dispatcher.Enqueued.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendMonth_AskedTwiceInARow_QueuesEachUnitOnlyOnce()
    {
        GiveTheSecondUnitAnEmail();
        await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        Result<SendMonthInvoicesResult> second = await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        second.Value.Should().Be(new SendMonthInvoicesResult(0, 0, 2, 0));
        _kit.Dispatcher.Enqueued.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendMonth_WhenAnotherRequestWonTheRace_FailsWithoutQueueingAnything()
    {
        _kit.Deliveries.AddFailure = InvoiceDeliveryErrors.AlreadyQueued;

        Result<SendMonthInvoicesResult> result = await SendHandler().Handle(new SendMonthInvoicesCommand("2026-10", Actor), default);

        result.Error.Should().Be(InvoiceDeliveryErrors.AlreadyQueued);
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
    }

    // ---- Reenviar uma unidade ----

    [Fact]
    public async Task Resend_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result result = await ResendHandler(kit).Handle(new ResendInvoiceCommand(_first.Id, Actor), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task Resend_AChargeThatDoesNotExist_Fails()
    {
        Result result = await ResendHandler().Handle(new ResendInvoiceCommand(Guid.CreateVersion7(), Actor), default);

        result.Error.Should().Be(PaymentErrors.ChargeNotFound);
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_ToAUnitWithoutAResponsibleWithAnEmail_Fails()
    {
        Result result = await ResendHandler().Handle(new ResendInvoiceCommand(_second.Id, Actor), default);

        result.Error.Should().Be(InvoiceDeliveryErrors.NoRecipient);
        _kit.Deliveries.Added.Should().BeEmpty();
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
        _kit.Parties.AskedAbout.Should().Equal(_second.UnitId);
    }

    [Fact]
    public async Task Resend_OfAUnitNeverSent_CreatesTheDeliveryAndQueuesIt()
    {
        Result result = await ResendHandler().Handle(new ResendInvoiceCommand(_first.Id, Actor), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Deliveries.Added.Should().ContainSingle().Which.Should().Match<InvoiceDelivery>(
            delivery => delivery.ChargeId == _first.Id && delivery.Status == InvoiceDeliveryStatus.Queued && delivery.RequestedBy == Actor);
        _kit.Dispatcher.Enqueued.Should().Equal((_kit.CondominiumId, _first.Id));
    }

    [Theory]
    [InlineData(InvoiceDeliveryStatus.Sent)]
    [InlineData(InvoiceDeliveryStatus.Failed)]
    public async Task Resend_OfASentOrFailedInvoice_QueuesItAgain(InvoiceDeliveryStatus status)
    {
        InvoiceDelivery delivery = _kit.Deliveries.Existing(_first, status, FinanceTestKit.Now.AddDays(-1));

        Result result = await ResendHandler().Handle(new ResendInvoiceCommand(_first.Id, Actor), default);

        result.IsSuccess.Should().BeTrue();
        delivery.Status.Should().Be(InvoiceDeliveryStatus.Queued);
        delivery.RequestedAt.Should().Be(FinanceTestKit.Now);
        _kit.Deliveries.Saved.Should().Equal(delivery);
        _kit.Deliveries.Added.Should().BeEmpty();
        _kit.Dispatcher.Enqueued.Should().Equal((_kit.CondominiumId, _first.Id));
    }

    [Fact]
    public async Task Resend_OfAnInvoiceAlreadyInTheQueue_FailsWithoutQueueingTwice()
    {
        _kit.Deliveries.Existing(_first, InvoiceDeliveryStatus.Queued, FinanceTestKit.Now.AddMinutes(-1));

        Result result = await ResendHandler().Handle(new ResendInvoiceCommand(_first.Id, Actor), default);

        result.Error.Should().Be(InvoiceDeliveryErrors.AlreadyQueued);
        _kit.Deliveries.Saved.Should().BeEmpty();
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_WhenAnotherRequestWonTheRace_Fails()
    {
        _kit.Deliveries.AddFailure = InvoiceDeliveryErrors.AlreadyQueued;

        Result result = await ResendHandler().Handle(new ResendInvoiceCommand(_first.Id, Actor), default);

        result.Error.Should().Be(InvoiceDeliveryErrors.AlreadyQueued);
        _kit.Dispatcher.Enqueued.Should().BeEmpty();
    }

    // ---- Ver como está cada unidade ----

    [Fact]
    public async Task List_WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<InvoiceDeliveriesView> result = await ListHandler(kit).Handle(new ListInvoiceDeliveriesQuery("2026-10"), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
    }

    [Fact]
    public async Task List_WithAnInvalidMonth_Fails()
    {
        Result<InvoiceDeliveriesView> result = await ListHandler().Handle(new ListInvoiceDeliveriesQuery("outubro"), default);

        result.Error.Should().Be(YearMonth.Parse("outubro").Error);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public async Task List_WithAnInvalidPage_Fails(int page, int pageSize)
    {
        Result<InvoiceDeliveriesView> result = await ListHandler().Handle(new ListInvoiceDeliveriesQuery("2026-10", page, pageSize), default);

        result.Error.Should().Be(InvoiceDeliveryErrors.PageInvalid);
    }

    [Fact]
    public async Task List_OfAMonthThatIsNotClosed_Fails()
    {
        Result<InvoiceDeliveriesView> result = await ListHandler().Handle(new ListInvoiceDeliveriesQuery("2026-09"), default);

        result.Error.Should().Be(ClosingErrors.NotClosed);
    }

    [Fact]
    public async Task List_ShowsEachUnitInOrderWithItsResponsibleAndItsState()
    {
        _kit.Deliveries.Existing(_first, InvoiceDeliveryStatus.Sent, FinanceTestKit.Now.AddDays(-1), sentAt: new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero));

        Result<InvoiceDeliveriesView> result = await ListHandler().Handle(new ListInvoiceDeliveriesQuery("2026-10"), default);

        InvoiceDeliveriesView view = result.Value;
        view.Month.Should().Be("2026-10");
        view.Total.Should().Be(2);
        view.Items.Select(item => item.Number).Should().Equal("101", "102");
        view.Items[0].Should().Be(new InvoiceDeliveryView(_first.Id, _first.UnitId, null, "101", "Maria Souza", InvoiceDeliveryState.Sent, new DateTimeOffset(2026, 11, 6, 9, 30, 0, TimeSpan.Zero), null));
        view.Items[1].Should().Be(new InvoiceDeliveryView(_second.Id, _second.UnitId, "A", "102", null, InvoiceDeliveryState.NoRecipient, null, null));
    }

    [Fact]
    public async Task List_CountsEachStateAcrossTheWholeMonth_NotJustThePage()
    {
        GiveTheSecondUnitAnEmail();
        _kit.Deliveries.Existing(_first, InvoiceDeliveryStatus.Failed, FinanceTestKit.Now.AddMinutes(-30), InvoiceDeliveryFailure.NotDelivered);

        Result<InvoiceDeliveriesView> result = await ListHandler().Handle(new ListInvoiceDeliveriesQuery("2026-10", 1, 1), default);

        result.Value.Items.Should().HaveCount(1);
        result.Value.Total.Should().Be(2);
        result.Value.Counts.Should().Equal(new Dictionary<InvoiceDeliveryState, int>
        {
            [InvoiceDeliveryState.NoRecipient] = 0,
            [InvoiceDeliveryState.NotSent] = 1,
            [InvoiceDeliveryState.Queued] = 0,
            [InvoiceDeliveryState.Sent] = 0,
            [InvoiceDeliveryState.Failed] = 1,
        });
    }

    [Fact]
    public async Task List_TheFailureReasonOnlyAppearsOnTheUnitsThatFailed()
    {
        GiveTheSecondUnitAnEmail();
        _kit.Deliveries.Existing(_first, InvoiceDeliveryStatus.Failed, FinanceTestKit.Now.AddMinutes(-30), InvoiceDeliveryFailure.NotDelivered);
        _kit.Deliveries.Existing(_second, InvoiceDeliveryStatus.Queued, FinanceTestKit.Now.AddMinutes(-1));

        Result<InvoiceDeliveriesView> result = await ListHandler().Handle(new ListInvoiceDeliveriesQuery("2026-10"), default);

        result.Value.Items[0].State.Should().Be(InvoiceDeliveryState.Failed);
        result.Value.Items[0].FailureReason.Should().Be(InvoiceDeliveryFailure.NotDelivered);
        result.Value.Items[1].State.Should().Be(InvoiceDeliveryState.Queued);
        result.Value.Items[1].FailureReason.Should().BeNull();
    }

    [Fact]
    public async Task List_PagesTheUnits()
    {
        GiveTheSecondUnitAnEmail();

        Result<InvoiceDeliveriesView> second = await ListHandler().Handle(new ListInvoiceDeliveriesQuery("2026-10", 2, 1), default);

        second.Value.Page.Should().Be(2);
        second.Value.PageSize.Should().Be(1);
        second.Value.Items.Should().ContainSingle().Which.Number.Should().Be("102");
        _kit.Parties.AskedAbout.Should().HaveCount(2);
    }
}
