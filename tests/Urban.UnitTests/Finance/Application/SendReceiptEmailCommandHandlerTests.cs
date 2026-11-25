using FluentAssertions;
using MediatR;
using Moq;
using Urban.Finance.Application.Receipts;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// O envio de verdade, o que roda no segundo plano. Outubro de 2026 está fechado, a cobrança é a da unidade 101 (600) e o
/// responsável, Maria, tem e-mail. A montagem do PDF é o <see cref="GetReceiptQuery"/>, que o handler pede pelo MediatR.
/// </summary>
public class SendReceiptEmailCommandHandlerTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];
    private static readonly DateOnly Today = new(2026, 11, 7);

    private readonly FinanceTestKit _kit = new();
    private readonly Mock<ISender> _sender = new();
    private readonly UnitCharge _charge;
    private readonly YearMonth _october = FinanceTestKit.M(2026, 10);

    public SendReceiptEmailCommandHandlerTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        _kit.NewEntry(_kit.NewFixed(group, _october, 1000m), _october);
        MonthClosing closing = _kit.CloseMonth(_october, new DateOnly(2026, 11, 10));
        _charge = closing.Charges.Single(charge => charge.Number == "101");
        _kit.Parties.Responsibles[_charge.UnitId] = new InvoiceResponsible("Maria Souza", "maria@exemplo.com");
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetReceiptQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ReceiptFile("recibo-000001.pdf", Pdf)));
    }

    private SendReceiptEmailCommandHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new SendReceiptEmailCommandHandler(used.Payments, used.Charges, used.Parties, _sender.Object, used.Emails, used.Tenant, used.Clock);
    }

    private Payment Paid(decimal amount = 250m) =>
        _kit.Payments.Existing(Payment.Register(
            _kit.CondominiumId, _charge.Id, _october, amount, new DateOnly(2026, 11, 5), PaymentMethod.Pix, "Anotação do síndico", Guid.CreateVersion7(), FinanceTestKit.Now, Today).Value);

    [Fact]
    public async Task WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result result = await Handler(kit).Handle(new SendReceiptEmailCommand(Guid.CreateVersion7()), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task APaymentThatDoesNotExist_Fails()
    {
        Result result = await Handler().Handle(new SendReceiptEmailCommand(Guid.CreateVersion7()), default);

        result.Error.Should().Be(PaymentErrors.NotFound);
        _kit.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendsTheReceiptAsAnAttachmentToTheResponsible()
    {
        Payment payment = Paid();

        Result result = await Handler().Handle(new SendReceiptEmailCommand(payment.Id), default);

        result.IsSuccess.Should().BeTrue();
        EmailMessage message = _kit.Emails.Sent.Should().ContainSingle().Subject;
        message.To.Should().Be("maria@exemplo.com");
        message.Subject.Should().Be("Recibo REC-000001 - Residencial Aurora");
        message.TextBody.Should().Contain("Olá, Maria Souza.").And.Contain("Valor recebido: R$ 250,00").And.Contain("Data do recebimento: 05/11/2026");
        message.TextBody.Should().Contain("da unidade 101").And.NotContain("Anotação do síndico");
        message.ReplyTo.Should().BeNull();
        EmailAttachment attachment = message.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("recibo-000001.pdf");
        attachment.Content.Should().Equal(Pdf);
    }

    [Fact]
    public async Task AsksForTheReceiptOfTheSamePayment_WithoutAResidentFilter()
    {
        Payment payment = Paid();

        await Handler().Handle(new SendReceiptEmailCommand(payment.Id), default);

        _sender.Verify(
            sender => sender.Send(It.Is<GetReceiptQuery>(query => query.PaymentId == payment.Id && query.ResidentUserId == null), It.IsAny<CancellationToken>()),
            Times.Once);
        _kit.Parties.AskedAbout.Should().Equal(_charge.UnitId);
        _kit.Parties.AskedOn.Should().Be(Today);
    }

    [Fact]
    public async Task AReceiptQueuedBeforeTheReversal_IsNotSentAnyMore()
    {
        Payment payment = Paid();
        payment.Reverse("Unidade errada", Guid.CreateVersion7(), FinanceTestKit.Now).IsSuccess.Should().BeTrue();

        Result result = await Handler().Handle(new SendReceiptEmailCommand(payment.Id), default);

        result.Error.Should().Be(ReceiptDeliveryErrors.PaymentReversed);
        _kit.Emails.Sent.Should().BeEmpty();
        _sender.Verify(sender => sender.Send(It.IsAny<GetReceiptQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenTheCondominiumDataIsMissing_FailsWithoutAnyEmail()
    {
        Payment payment = Paid();
        _kit.Parties.Condominium = null;

        Result result = await Handler().Handle(new SendReceiptEmailCommand(payment.Id), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        _kit.Emails.Sent.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutAResponsibleWithAnEmail_FailsForGood_WithoutAnyEmailOrRetry(bool responsibleWithoutEmail)
    {
        Payment payment = Paid();
        _kit.Parties.Responsibles.Remove(_charge.UnitId);

        if (responsibleWithoutEmail)
        {
            _kit.Parties.Responsibles[_charge.UnitId] = new InvoiceResponsible("Maria Souza", null);
        }

        Result result = await Handler().Handle(new SendReceiptEmailCommand(payment.Id), default);

        result.Error.Should().Be(ReceiptDeliveryErrors.NoRecipient);
        _kit.Emails.Sent.Should().BeEmpty();
        _sender.Verify(sender => sender.Send(It.IsAny<GetReceiptQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenThePdfCannotBeBuilt_FailsForGood_WithoutAnyEmail()
    {
        Payment payment = Paid();
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetReceiptQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ReceiptFile>(PaymentErrors.NotFound));

        Result result = await Handler().Handle(new SendReceiptEmailCommand(payment.Id), default);

        result.Error.Should().Be(PaymentErrors.NotFound);
        _kit.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheProviderRefuses_TheFailureReachesTheQueueSoItRetries()
    {
        Payment payment = Paid();
        _kit.Emails.Refuse = true;

        Func<Task> run = () => Handler().Handle(new SendReceiptEmailCommand(payment.Id), default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
    }
}
