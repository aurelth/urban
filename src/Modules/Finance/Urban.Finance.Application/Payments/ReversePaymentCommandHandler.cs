using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Payments;

public sealed class ReversePaymentCommandHandler(IPayments payments, ITenantContext tenant, IClock clock) : IRequestHandler<ReversePaymentCommand, Result>
{
    public async Task<Result> Handle(ReversePaymentCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure(FinanceErrors.CondominiumNotFound);
        }

        if (await payments.FindAsync(request.PaymentId, cancellationToken) is not { } payment)
        {
            return Result.Failure(PaymentErrors.NotFound);
        }

        Result reversed = payment.Reverse(request.Reason, request.ActorUserId, clock.UtcNow);

        return reversed.IsFailure ? reversed : await payments.ReverseAsync(payment, cancellationToken);
    }
}
