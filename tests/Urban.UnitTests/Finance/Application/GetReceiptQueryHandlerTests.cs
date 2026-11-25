using System.Text.Json;
using FluentAssertions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Application.Receipts;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// O PDF do recibo de um pagamento. Hoje é 7/11/2026; outubro está fechado com duas cobranças, 600 (unidade 101) e 400 (bloco A,
/// 102). O recibo é o comprovante de UM pagamento: diz o que foi recebido e o que ainda faltava depois dele, e some com qualquer
/// anotação interna (observação do pagamento, motivo do estorno).
/// </summary>
public class GetReceiptQueryHandlerTests
{
    private static readonly Guid Manager = Guid.CreateVersion7();
    private static readonly Guid Resident = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 11, 7);

    private readonly FinanceTestKit _kit = new();
    private readonly UnitCharge _first;
    private readonly UnitCharge _second;
    private readonly YearMonth _october = FinanceTestKit.M(2026, 10);

    public GetReceiptQueryHandlerTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        Expense expense = _kit.NewFixed(group, _october, 1000m);
        _kit.NewEntry(expense, _october);
        MonthClosing closing = _kit.CloseMonth(_october, new DateOnly(2026, 11, 10));
        _first = closing.Charges.Single(charge => charge.Number == "101");
        _second = closing.Charges.Single(charge => charge.Number == "102");
    }

    private GetReceiptQueryHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new GetReceiptQueryHandler(used.Payments, used.Charges, used.Parties, used.Housing, used.ReceiptRenderer, used.Tenant, used.Clock);
    }

    private Payment Paid(UnitCharge charge, decimal amount, PaymentMethod method = PaymentMethod.Pix, string? note = null, DateOnly? receivedOn = null) =>
        _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, charge.Id, _october, amount, receivedOn ?? new DateOnly(2026, 11, 5), method, note, Manager, FinanceTestKit.Now, Today).Value);

    private async Task<ReceiptDocument> RenderedAsync(Payment payment)
    {
        _kit.ReceiptRenderer.Rendered.Clear();
        (await Handler().Handle(new GetReceiptQuery(payment.Id), default)).IsSuccess.Should().BeTrue();

        return _kit.ReceiptRenderer.Rendered.Single();
    }

    // ---- Quando não há recibo ----

    [Fact]
    public async Task Handle_WithoutACondominium_Fails()
    {
        Payment payment = Paid(_first, 100m);
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<ReceiptFile> result = await Handler(kit).Handle(new GetReceiptQuery(payment.Id), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.ReceiptRenderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_APaymentThatDoesNotExist_Fails()
    {
        Result<ReceiptFile> result = await Handler().Handle(new GetReceiptQuery(Guid.CreateVersion7()), default);

        result.Error.Should().Be(PaymentErrors.NotFound);
        _kit.ReceiptRenderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheCondominiumDataIsMissing_Fails()
    {
        Payment payment = Paid(_first, 100m);
        _kit.Parties.Condominium = null;

        Result<ReceiptFile> result = await Handler().Handle(new GetReceiptQuery(payment.Id), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        _kit.ReceiptRenderer.Rendered.Should().BeEmpty();
    }

    // ---- O arquivo e o documento ----

    [Fact]
    public async Task Handle_ReturnsTheRenderedFile_WithTheNumberInTheName()
    {
        Paid(_first, 100m);
        Payment second = Paid(_first, 50m);

        Result<ReceiptFile> result = await Handler().Handle(new GetReceiptQuery(second.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("recibo-000002.pdf");
        result.Value.Content.Should().Equal(FakeReceiptRenderer.Output);
        _kit.ReceiptRenderer.Rendered.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_FillsTheDocumentFromThePaymentTheChargeAndTheCondominium()
    {
        Payment payment = Paid(_first, 250.50m, PaymentMethod.Transfer, receivedOn: new DateOnly(2026, 11, 4));

        ReceiptDocument document = await RenderedAsync(payment);

        document.Code.Should().Be("REC-000001");
        document.CondominiumName.Should().Be("Residencial Aurora");
        document.CondominiumCnpj.Should().Be("12.345.678/0001-95");
        document.CondominiumAddress.Should().Be("Rua das Flores, 100 - Centro - Blumenau/SC - 89010-000");
        document.UnitLabel.Should().Be("101");
        document.Month.Should().Be(_october);
        document.InvoiceReference.Should().Be(InvoiceText.Reference(_october, _first.Id));
        document.ChargeTotal.Should().Be(600m);
        document.Amount.Should().Be(250.50m);
        document.ReceivedOn.Should().Be(new DateOnly(2026, 11, 4));
        document.Method.Should().Be(PaymentMethod.Transfer);
        document.IssuedOn.Should().Be(Today);
        document.CanceledOn.Should().BeNull();
        document.Manager.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_TheUnitLabelCarriesTheBlock()
    {
        Payment payment = Paid(_second, 100m);

        (await RenderedAsync(payment)).UnitLabel.Should().Be("Bloco A · 102");
    }

    [Fact]
    public async Task Handle_IssuedOnIsTheDayOfTheRegistrationInBrasilia()
    {
        Payment payment = _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, _first.Id, _october, 100m, new DateOnly(2026, 11, 5), PaymentMethod.Pix, null, Manager, new DateTimeOffset(2026, 11, 8, 1, 30, 0, TimeSpan.Zero), Today.AddDays(1)).Value);

        (await RenderedAsync(payment)).IssuedOn.Should().Be(Today);
    }

    // ---- O saldo depois deste pagamento ----

    [Fact]
    public async Task Handle_TheBalanceCountsOnlyThePaymentsUpToThisOne()
    {
        Payment one = Paid(_first, 200m);
        Payment two = Paid(_first, 150m);
        Payment three = Paid(_first, 50m);

        ReceiptDocument first = await RenderedAsync(one);
        ReceiptDocument second = await RenderedAsync(two);
        ReceiptDocument third = await RenderedAsync(three);

        (first.PaidSoFar, first.Balance).Should().Be((200m, 400m));
        (second.PaidSoFar, second.Balance).Should().Be((350m, 250m));
        (third.PaidSoFar, third.Balance).Should().Be((400m, 200m));
    }

    [Fact]
    public async Task Handle_APaymentThatSettlesTheCharge_LeavesNoBalance()
    {
        Payment payment = Paid(_first, 600m);

        ReceiptDocument document = await RenderedAsync(payment);

        document.Balance.Should().Be(0m);
        document.PaidSoFar.Should().Be(600m);
    }

    [Fact]
    public async Task Handle_PaymentsOfOtherChargesNeverCount()
    {
        Paid(_second, 300m);
        Payment payment = Paid(_first, 100m);

        (await RenderedAsync(payment)).PaidSoFar.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_AReversedEarlierPaymentDoesNotCountForALaterReceipt()
    {
        Payment wrong = Paid(_first, 200m);
        wrong.Reverse("Unidade errada", Manager, FinanceTestKit.Now.AddHours(1));
        Payment right = Paid(_first, 100m);

        ReceiptDocument document = await RenderedAsync(right);

        document.PaidSoFar.Should().Be(100m);
        document.Balance.Should().Be(500m);
    }

    // ---- Recibo cancelado ----

    [Fact]
    public async Task Handle_AReversedPayment_GetsACanceledReceipt_WithTheDayOfTheReversal()
    {
        Payment payment = Paid(_first, 200m);
        payment.Reverse("Unidade errada", Manager, FinanceTestKit.Now.AddDays(1));

        ReceiptDocument document = await RenderedAsync(payment);

        document.CanceledOn.Should().Be(Today.AddDays(1));
        document.Code.Should().Be("REC-000001");
        document.Amount.Should().Be(200m);
        document.PaidSoFar.Should().Be(0m);
        document.Balance.Should().Be(600m);
    }

    [Fact]
    public async Task Handle_TheNoteAndTheReversalReason_NeverReachTheDocument()
    {
        Payment payment = Paid(_first, 200m, note: "Anotacao interna do sindico");
        payment.Reverse("Motivo sigiloso do estorno", Manager, FinanceTestKit.Now.AddDays(1));

        string json = JsonSerializer.Serialize(await RenderedAsync(payment));

        json.Should().NotContain("Anotacao interna").And.NotContain("Motivo sigiloso");
    }

    // ---- Quem recebeu e de quem ----

    [Fact]
    public async Task Handle_NamesTheResponsibleOfTheUnit_AsOfTodayInBrasilia()
    {
        _kit.Parties.Responsibles[_first.UnitId] = new InvoiceResponsible("Marina Souza", "marina@example.com");
        Payment payment = Paid(_first, 100m);

        (await RenderedAsync(payment)).ResponsibleName.Should().Be("Marina Souza");
        _kit.Parties.AskedOn.Should().Be(Today);
        _kit.Parties.AskedAbout.Should().Equal(_first.UnitId);
    }

    [Fact]
    public async Task Handle_AUnitWithoutResponsible_StillGetsTheReceipt()
    {
        Payment payment = Paid(_first, 100m);

        Result<ReceiptFile> result = await Handler().Handle(new GetReceiptQuery(payment.Id), default);

        result.IsSuccess.Should().BeTrue();
        _kit.ReceiptRenderer.Rendered.Single().ResponsibleName.Should().BeNull();
    }

    // ---- O morador pede o próprio recibo ----

    private static ResidentUnit HomeOf(UnitCharge charge) => new(charge.UnitId, charge.Block, charge.Number);

    private Task<Result<ReceiptFile>> AskAsResident(Payment payment, Guid? userId = null) =>
        Handler().Handle(new GetReceiptQuery(payment.Id, userId ?? Resident), default);

    [Fact]
    public async Task Handle_AResidentGetsTheReceiptOfAPaymentOfHisUnit_ExactlyTheSameDocumentAsTheManager()
    {
        _kit.Housing.Lives(Resident, HomeOf(_second));
        Payment payment = Paid(_second, 100m);
        ReceiptDocument forTheManager = await RenderedAsync(payment);
        _kit.ReceiptRenderer.Rendered.Clear();

        Result<ReceiptFile> result = await AskAsResident(payment);

        result.IsSuccess.Should().BeTrue();
        _kit.ReceiptRenderer.Rendered.Single().Should().BeEquivalentTo(forTheManager);
    }

    [Fact]
    public async Task Handle_AResidentCannotGetTheReceiptOfAnotherUnit_AndItLooksLikeThePaymentDoesNotExist()
    {
        _kit.Housing.Lives(Resident, HomeOf(_first));
        Payment neighbour = Paid(_second, 100m);

        Result<ReceiptFile> other = await AskAsResident(neighbour);
        Result<ReceiptFile> missing = await Handler().Handle(new GetReceiptQuery(Guid.CreateVersion7(), Resident), default);

        other.Error.Should().Be(PaymentErrors.NotFound);
        missing.Error.Should().Be(PaymentErrors.NotFound);
        _kit.ReceiptRenderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AResidentWhoOnlyLivesAsDependentOrWhoLivesNowhereGetsNothing()
    {
        Guid dependent = Guid.CreateVersion7();
        _kit.Housing.LivesAsDependent(dependent);
        Payment payment = Paid(_first, 100m);

        (await AskAsResident(payment, dependent)).Error.Should().Be(PaymentErrors.NotFound);
        (await AskAsResident(payment, Guid.CreateVersion7())).Error.Should().Be(PaymentErrors.NotFound);
        _kit.ReceiptRenderer.Rendered.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TheHousingIsAskedAboutTheAccountOfTheToken_AsOfTodayInBrasilia()
    {
        _kit.Housing.Lives(Resident, HomeOf(_first));
        Payment payment = Paid(_first, 100m);

        await AskAsResident(payment);

        _kit.Housing.AskedAbout.Should().Be(Resident);
        _kit.Housing.AskedOn.Should().Be(Today);
    }

    [Fact]
    public async Task Handle_TheManagerAsksWithoutAnAccount_AndTheHousingIsNeverConsulted()
    {
        Payment payment = Paid(_first, 100m);

        await Handler().Handle(new GetReceiptQuery(payment.Id), default);

        _kit.Housing.Calls.Should().Be(0);
    }
}
