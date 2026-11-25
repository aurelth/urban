using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

/// <summary>
/// Confere o pedido do síndico e põe o envio na fila, sem enviar nada na requisição. Nada fica gravado: não há histórico de envios do
/// recibo, e o síndico pode pedir de novo quando quiser. Quem recebe é dito na resposta para a tela confirmar o destino.
/// </summary>
public sealed class QueueReceiptEmailCommandHandler(
    IPayments payments,
    ICharges charges,
    IInvoiceParties parties,
    IReceiptEmailDispatcher dispatcher,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<QueueReceiptEmailCommand, Result<ReceiptRecipient>>
{
    public async Task<Result<ReceiptRecipient>> Handle(QueueReceiptEmailCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<ReceiptRecipient>(FinanceErrors.CondominiumNotFound);
        }

        Result<ReceiptTarget> target = await ReceiptTarget.FindAsync(payments, charges, parties, request.PaymentId, FinanceCalendar.Today(clock.UtcNow), cancellationToken);

        if (target.IsFailure)
        {
            return Result.Failure<ReceiptRecipient>(target.Error);
        }

        await dispatcher.EnqueueAsync(condominiumId, request.PaymentId, cancellationToken);

        return Result.Success(new ReceiptRecipient(target.Value.Responsible.Name));
    }
}
