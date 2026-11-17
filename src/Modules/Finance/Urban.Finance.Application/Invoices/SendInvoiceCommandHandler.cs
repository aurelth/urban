using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Invoices;

/// <summary>
/// O envio de verdade, no segundo plano. Quem já recebeu não recebe de novo (uma tentativa velha da fila que chega depois do
/// reenvio é ignorada), e o resultado de cada tentativa fica gravado. Falha do provedor é gravada e relançada para a fila tentar
/// de novo (a mensagem da exceção não leva o endereço, então o log da fila não expõe quem recebe); falha que repetir não adianta (sem e-mail, PDF que não monta) é gravada e devolvida como erro, sem retentativa.
/// </summary>
public sealed class SendInvoiceCommandHandler(
    IInvoiceDeliveries deliveries,
    IInvoiceParties parties,
    ISender sender,
    IEmailSender email,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<SendInvoiceCommand, Result>
{
    public async Task<Result> Handle(SendInvoiceCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        if (await deliveries.FindChargeAsync(request.ChargeId, cancellationToken) is not { } charge)
        {
            return Result.Failure(PaymentErrors.ChargeNotFound);
        }

        if (await deliveries.FindAsync(request.ChargeId, cancellationToken) is not { } delivery || delivery.Status == InvoiceDeliveryStatus.Sent)
        {
            return Result.Success();
        }

        if (await parties.GetCondominiumAsync(cancellationToken) is not { } condominium)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        IReadOnlyDictionary<Guid, InvoiceResponsible> responsibles = await parties.GetResponsiblesAsync([charge.UnitId], FinanceCalendar.Today(clock.UtcNow), cancellationToken);

        if (!responsibles.TryGetValue(charge.UnitId, out InvoiceResponsible? responsible) || responsible.Email is not { } to)
        {
            return await FailAsync(delivery, InvoiceDeliveryFailure.NoRecipient, InvoiceDeliveryErrors.NoRecipient, cancellationToken);
        }

        Result<InvoiceFile> invoice = await sender.Send(new GetInvoiceQuery(request.ChargeId), cancellationToken);

        if (invoice.IsFailure)
        {
            return await FailAsync(delivery, InvoiceDeliveryFailure.InvoiceUnavailable, invoice.Error, cancellationToken);
        }

        EmailMessage message = new(
            to,
            InvoiceEmail.Subject(charge, condominium),
            InvoiceEmail.Body(charge, condominium, responsible),
            Attachments: [new EmailAttachment(invoice.Value.FileName, invoice.Value.Content)]);

        try
        {
            await email.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            await RecordFailureAsync(delivery, InvoiceDeliveryFailure.NotDelivered, cancellationToken);

            throw;
        }

        delivery.MarkSent(clock.UtcNow);
        await deliveries.SaveAsync(delivery, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> FailAsync(InvoiceDelivery delivery, string reason, Error error, CancellationToken cancellationToken)
    {
        await RecordFailureAsync(delivery, reason, cancellationToken);

        return Result.Failure(error);
    }

    private async Task RecordFailureAsync(InvoiceDelivery delivery, string reason, CancellationToken cancellationToken)
    {
        delivery.MarkFailed(reason, clock.UtcNow);
        await deliveries.SaveAsync(delivery, cancellationToken);
    }
}
