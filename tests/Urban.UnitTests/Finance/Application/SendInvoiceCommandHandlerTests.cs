using FluentAssertions;
using MediatR;
using Moq;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// O envio de verdade, o que roda no segundo plano. Outubro de 2026 está fechado, a cobrança é a da unidade 101 (600) e o responsável,
/// Maria, tem e-mail. A montagem do PDF é o <see cref="GetInvoiceQuery"/>, que o handler pede pelo MediatR.
/// </summary>
public class SendInvoiceCommandHandlerTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    private readonly FinanceTestKit _kit = new();
    private readonly Mock<ISender> _sender = new();
    private readonly UnitCharge _charge;
    private InvoiceDelivery _delivery;

    public SendInvoiceCommandHandlerTests()
    {
        ExpenseGroup group = _kit.NewGroup();
        Expense expense = _kit.NewFixed(group, FinanceTestKit.M(2026, 10), 1000m);
        _kit.NewEntry(expense, FinanceTestKit.M(2026, 10));
        MonthClosing closing = _kit.CloseMonth(FinanceTestKit.M(2026, 10), new DateOnly(2026, 11, 10));
        _charge = closing.Charges.Single(charge => charge.Number == "101");
        _kit.Parties.Responsibles[_charge.UnitId] = new InvoiceResponsible("Maria Souza", "maria@exemplo.com");
        _delivery = _kit.Deliveries.Existing(_charge, InvoiceDeliveryStatus.Queued, FinanceTestKit.Now.AddMinutes(-1));
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetInvoiceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new InvoiceFile("fatura-2026-10-101.pdf", Pdf)));
    }

    private SendInvoiceCommandHandler Handler(FinanceTestKit? kit = null)
    {
        FinanceTestKit used = kit ?? _kit;

        return new SendInvoiceCommandHandler(used.Deliveries, used.Parties, _sender.Object, used.Emails, used.Tenant, used.Clock);
    }

    [Fact]
    public async Task WithoutACondominium_Fails()
    {
        FinanceTestKit kit = new FinanceTestKit().WithoutATenant();

        Result result = await Handler(kit).Handle(new SendInvoiceCommand(_charge.Id), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        kit.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AChargeThatDoesNotExist_Fails()
    {
        Result result = await Handler().Handle(new SendInvoiceCommand(Guid.CreateVersion7()), default);

        result.Error.Should().Be(PaymentErrors.ChargeNotFound);
        _kit.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WithoutADeliveryRecord_DoesNothing()
    {
        FinanceTestKit kit = new();
        ExpenseGroup group = kit.NewGroup();
        kit.NewEntry(kit.NewFixed(group, FinanceTestKit.M(2026, 10), 1000m), FinanceTestKit.M(2026, 10));
        UnitCharge charge = kit.CloseMonth(FinanceTestKit.M(2026, 10)).Charges.First();

        Result result = await Handler(kit).Handle(new SendInvoiceCommand(charge.Id), default);

        result.IsSuccess.Should().BeTrue();
        kit.Emails.Sent.Should().BeEmpty();
        _sender.Verify(sender => sender.Send(It.IsAny<GetInvoiceQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnInvoiceAlreadySent_IsNotSentAgain_SoAStaleAttemptFromTheQueueIsHarmless()
    {
        _delivery = _kit.Deliveries.Existing(_charge, InvoiceDeliveryStatus.Sent, FinanceTestKit.Now.AddHours(-1));

        Result result = await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        result.IsSuccess.Should().BeTrue();
        _kit.Emails.Sent.Should().BeEmpty();
        _kit.Deliveries.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task SendsTheInvoiceAsAnAttachmentToTheResponsible_AndMarksItSent()
    {
        Result result = await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        result.IsSuccess.Should().BeTrue();
        EmailMessage message = _kit.Emails.Sent.Should().ContainSingle().Subject;
        message.To.Should().Be("maria@exemplo.com");
        message.Subject.Should().Be("Fatura de Outubro de 2026 - Residencial Aurora");
        message.TextBody.Should().Contain("Olá, Maria Souza.").And.Contain("Valor: R$ 600,00").And.Contain("Vencimento: 10/11/2026");
        message.ReplyTo.Should().BeNull();
        EmailAttachment attachment = message.Attachments.Should().ContainSingle().Subject;
        attachment.FileName.Should().Be("fatura-2026-10-101.pdf");
        attachment.Content.Should().Equal(Pdf);
        _delivery.Status.Should().Be(InvoiceDeliveryStatus.Sent);
        _delivery.SentAt.Should().Be(FinanceTestKit.Now);
        _delivery.Attempts.Should().Be(1);
        _kit.Deliveries.Saved.Should().Equal(_delivery);
    }

    [Fact]
    public async Task AskedForTheInvoiceOfTheSameCharge()
    {
        await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        _sender.Verify(sender => sender.Send(It.Is<GetInvoiceQuery>(query => query.ChargeId == _charge.Id), It.IsAny<CancellationToken>()), Times.Once);
        _kit.Parties.AskedAbout.Should().Equal(_charge.UnitId);
        _kit.Parties.AskedOn.Should().Be(new DateOnly(2026, 11, 7));
    }

    [Fact]
    public async Task ARetryAfterAFailure_SendsAndClearsTheFailure()
    {
        _delivery = _kit.Deliveries.Existing(_charge, InvoiceDeliveryStatus.Failed, FinanceTestKit.Now.AddMinutes(-10));

        Result result = await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        result.IsSuccess.Should().BeTrue();
        _delivery.Status.Should().Be(InvoiceDeliveryStatus.Sent);
        _delivery.FailureReason.Should().BeNull();
        _kit.Emails.Sent.Should().HaveCount(1);
    }

    [Fact]
    public async Task WhenTheCondominiumDataIsMissing_FailsWithoutMarkingTheDelivery()
    {
        _kit.Parties.Condominium = null;

        Result result = await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        result.Error.Should().Be(FinanceErrors.CondominiumNotFound);
        _kit.Emails.Sent.Should().BeEmpty();
        _kit.Deliveries.Saved.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutAResponsibleWithAnEmail_MarksItFailedForGood_WithoutAnyEmailOrRetry(bool responsibleWithoutEmail)
    {
        _kit.Parties.Responsibles.Remove(_charge.UnitId);

        if (responsibleWithoutEmail)
        {
            _kit.Parties.Responsibles[_charge.UnitId] = new InvoiceResponsible("Maria Souza", null);
        }

        Result result = await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        result.Error.Should().Be(InvoiceDeliveryErrors.NoRecipient);
        _delivery.Status.Should().Be(InvoiceDeliveryStatus.Failed);
        _delivery.FailureReason.Should().Be(InvoiceDeliveryFailure.NoRecipient);
        _kit.Emails.Sent.Should().BeEmpty();
        _sender.Verify(sender => sender.Send(It.IsAny<GetInvoiceQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenThePdfCannotBeBuilt_MarksItFailedForGood_WithoutAnyEmail()
    {
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetInvoiceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<InvoiceFile>(PaymentErrors.ChargeNotFound));

        Result result = await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        result.Error.Should().Be(PaymentErrors.ChargeNotFound);
        _delivery.Status.Should().Be(InvoiceDeliveryStatus.Failed);
        _delivery.FailureReason.Should().Be(InvoiceDeliveryFailure.InvoiceUnavailable);
        _kit.Emails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenTheProviderRefuses_RecordsTheFailureAndRethrowsSoTheQueueRetries()
    {
        _kit.Emails.Refuse = true;

        Func<Task> run = () => Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        await run.Should().ThrowAsync<EmailDeliveryException>();
        _delivery.Status.Should().Be(InvoiceDeliveryStatus.Failed);
        _delivery.FailureReason.Should().Be(InvoiceDeliveryFailure.NotDelivered);
        _delivery.Attempts.Should().Be(1);
        _kit.Deliveries.Saved.Should().Equal(_delivery);
    }

    [Fact]
    public async Task TheRetryAfterARefusal_SendsAndEndsSent()
    {
        _kit.Emails.Refuse = true;
        await FluentActions.Awaiting(() => Handler().Handle(new SendInvoiceCommand(_charge.Id), default)).Should().ThrowAsync<EmailDeliveryException>();
        _kit.Emails.Refuse = false;

        Result result = await Handler().Handle(new SendInvoiceCommand(_charge.Id), default);

        result.IsSuccess.Should().BeTrue();
        _delivery.Status.Should().Be(InvoiceDeliveryStatus.Sent);
        _delivery.Attempts.Should().Be(2);
    }
}
