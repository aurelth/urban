using FluentAssertions;
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
/// O pedido do síndico para mandar o recibo por e-mail. Hoje é 7/11/2026; outubro está fechado e a cobrança é a da unidade 101
/// (600), cujo responsável, Maria, tem e-mail. O pedido só confere e põe na fila: quem envia é o segundo plano.
/// </summary>
public class QueueReceiptEmailCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 11, 7);

    private readonly FinanceTestKit _kit = new();
    private readonly UnitCharge _charge;
    private readonly YearMonth _october = FinanceTestKit.M(2026, 10);

    public QueueReceiptEmailCommandHandlerTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        _kit.NewEntry(_kit.NewFixed(group, _october, 1000m), _october);
        MonthClosing closing = _kit.CloseMonth(_october, new DateOnly(2026, 11, 10));
        _charge = closing.Charges.Single(charge => charge.Number == "101");
        _kit.Parties.Responsibles[_charge.UnitId] = new InvoiceResponsible("Maria Souza", "maria@exemplo.com");
    }

    private QueueReceiptEmailCommandHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new QueueReceiptEmailCommandHandler(used.Payments, used.Charges, used.Parties, used.ReceiptDispatcher, used.Tenant, used.Clock);
    }

    private Payment Paid(decimal amount = 100m) =>
        _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, _charge.Id, _october, amount, new DateOnly(2026, 11, 5), PaymentMethod.Pix, null, Guid.CreateVersion7(), FinanceTestKit.Now, Today).Value);

    [Fact]
    public async Task WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result<ReceiptRecipient> result = await Handler(kit).Handle(new QueueReceiptEmailCommand(Guid.CreateVersion7()), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.ReceiptDispatcher.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task APaymentThatDoesNotExist_Fails()
    {
        Result<ReceiptRecipient> result = await Handler().Handle(new QueueReceiptEmailCommand(Guid.CreateVersion7()), default);

        result.Error.Should().Be(PaymentErrors.NotFound);
        _kit.ReceiptDispatcher.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task AReversedPayment_IsNotSent_BecauseItsReceiptIsCanceled()
    {
        Payment payment = Paid();
        payment.Reverse("Unidade errada", Guid.CreateVersion7(), FinanceTestKit.Now).IsSuccess.Should().BeTrue();

        Result<ReceiptRecipient> result = await Handler().Handle(new QueueReceiptEmailCommand(payment.Id), default);

        result.Error.Should().Be(ReceiptDeliveryErrors.PaymentReversed);
        _kit.ReceiptDispatcher.Enqueued.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutAResponsibleWithAnEmail_FailsAndQueuesNothing(bool responsibleWithoutEmail)
    {
        Payment payment = Paid();
        _kit.Parties.Responsibles.Remove(_charge.UnitId);

        if (responsibleWithoutEmail)
        {
            _kit.Parties.Responsibles[_charge.UnitId] = new InvoiceResponsible("Maria Souza", null);
        }

        Result<ReceiptRecipient> result = await Handler().Handle(new QueueReceiptEmailCommand(payment.Id), default);

        result.Error.Should().Be(ReceiptDeliveryErrors.NoRecipient);
        _kit.ReceiptDispatcher.Enqueued.Should().BeEmpty();
    }

    [Fact]
    public async Task QueuesTheSendingWithTheCondominiumAndThePayment_AndTellsWhoWillReceiveIt()
    {
        Payment payment = Paid();

        Result<ReceiptRecipient> result = await Handler().Handle(new QueueReceiptEmailCommand(payment.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Maria Souza");
        _kit.ReceiptDispatcher.Enqueued.Should().Equal((_kit.CondominiumId, payment.Id));
    }

    [Fact]
    public async Task ItNeverSendsTheEmailItself_ThatIsTheBackgroundJobsWork()
    {
        Payment payment = Paid();

        await Handler().Handle(new QueueReceiptEmailCommand(payment.Id), default);

        _kit.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AsksForTheResponsibleOfTheUnitOfTheCharge_OnTheDayInBrasilia()
    {
        Payment payment = Paid();

        await Handler().Handle(new QueueReceiptEmailCommand(payment.Id), default);

        _kit.Parties.AskedAbout.Should().Equal(_charge.UnitId);
        _kit.Parties.AskedOn.Should().Be(Today);
    }

    [Fact]
    public async Task AskingAgain_QueuesAgain_BecauseTheManagerMayResend()
    {
        Payment payment = Paid();

        await Handler().Handle(new QueueReceiptEmailCommand(payment.Id), default);
        await Handler().Handle(new QueueReceiptEmailCommand(payment.Id), default);

        _kit.ReceiptDispatcher.Enqueued.Should().HaveCount(2);
    }
}
