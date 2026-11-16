using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Instructions;

public sealed class GetPaymentInstructionsQueryHandler(IPaymentInstructionsStore store, ITenantContext tenant)
    : IRequestHandler<GetPaymentInstructionsQuery, Result<PaymentInstructionsView>>
{
    public async Task<Result<PaymentInstructionsView>> Handle(GetPaymentInstructionsQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<PaymentInstructionsView>(FinanceErrors.CondominiumNotFound);
        }

        return Result.Success(PaymentInstructionsView.From(await store.FindAsync(cancellationToken)));
    }
}
