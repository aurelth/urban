using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

public sealed class QueueReceiptEmailCommandHandler(
    IPayments payments,
    ICharges charges,
    IInvoiceParties parties,
    IReceiptEmailDispatcher dispatcher,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<QueueReceiptEmailCommand, Result<ReceiptRecipient>>
{
    public Task<Result<ReceiptRecipient>> Handle(QueueReceiptEmailCommand request, CancellationToken cancellationToken)
    {
        _ = (payments, charges, parties, dispatcher, tenant, clock);

        return Task.FromResult(Result.Failure<ReceiptRecipient>(FinanceErrors.CondominiumNotFound));
    }
}
