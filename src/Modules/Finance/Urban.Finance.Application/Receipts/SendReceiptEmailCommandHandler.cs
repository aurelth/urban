using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Application.Invoices;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

/// <summary>
/// O envio de verdade, no segundo plano. Confere de novo o que a fila conferiu (entre o pedido e a execução o pagamento pode ter sido
/// estornado ou o responsável trocado) e só então monta o PDF e envia. Falha do provedor é relançada para a fila tentar de novo (a
/// mensagem da exceção não leva o endereço); falha que repetir não adianta (sem e-mail, estorno, PDF que não monta) volta como erro, sem retentativa.
/// </summary>
public sealed class SendReceiptEmailCommandHandler(
    IPayments payments,
    ICharges charges,
    IInvoiceParties parties,
    ISender sender,
    IEmailSender email,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<SendReceiptEmailCommand, Result>
{
    public async Task<Result> Handle(SendReceiptEmailCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        Result<ReceiptTarget> target = await ReceiptTarget.FindAsync(payments, charges, parties, request.PaymentId, FinanceCalendar.Today(clock.UtcNow), cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure(target.Error);
        }

        if (await parties.GetCondominiumAsync(cancellationToken) is not { } condominium)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        Result<ReceiptFile> receipt = await sender.Send(new GetReceiptQuery(request.PaymentId), cancellationToken);

        if (receipt.IsFailure)
        {
            return Result.Failure(receipt.Error);
        }

        ReceiptTarget found = target.Value;
        await email.SendAsync(
            new EmailMessage(
                found.Email,
                ReceiptEmail.Subject(found.Payment, condominium),
                ReceiptEmail.Body(found.Payment, InvoiceText.Unit(found.Detail.Charge.Block, found.Detail.Charge.Number), found.Detail.Month, condominium, found.Responsible),
                Attachments: [new EmailAttachment(receipt.Value.FileName, receipt.Value.Content)]),
            cancellationToken);

        return Result.Success();
    }
}
