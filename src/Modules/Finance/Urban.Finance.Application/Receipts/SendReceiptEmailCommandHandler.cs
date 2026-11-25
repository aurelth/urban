using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

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
    public Task<Result> Handle(SendReceiptEmailCommand request, CancellationToken cancellationToken)
    {
        _ = (payments, charges, parties, sender, email, tenant, clock);

        return Task.FromResult(Result.Failure(FinanceErrors.CondominiumNotFound));
    }
}
