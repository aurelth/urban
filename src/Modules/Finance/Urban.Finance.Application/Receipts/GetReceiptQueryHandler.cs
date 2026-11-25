using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;
using Urban.SharedKernel.Contracts;

namespace Urban.Finance.Application.Receipts;

public sealed class GetReceiptQueryHandler(
    IPayments payments,
    ICharges charges,
    IInvoiceParties parties,
    IResidentHousing housing,
    IReceiptRenderer renderer,
    ITenantContext tenant,
    IClock clock)
    : IRequestHandler<GetReceiptQuery, Result<ReceiptFile>>
{
    public Task<Result<ReceiptFile>> Handle(GetReceiptQuery request, CancellationToken cancellationToken)
    {
        _ = (payments, charges, parties, housing, renderer, tenant, clock);

        throw new NotImplementedException();
    }
}
