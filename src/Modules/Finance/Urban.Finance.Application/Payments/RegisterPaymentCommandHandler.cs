using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Payments;

/// <summary>
/// Confere o pagamento no Domain e grava. Quem garante que ele cabe no saldo é a gravação, com a cobrança travada, porque uma
/// conferência feita antes não vale contra um segundo pagamento ao mesmo tempo. Não usa <c>IAtomicWork</c>: é um único contexto.
/// </summary>
public sealed class RegisterPaymentCommandHandler(IPayments payments, ITenantContext tenant, IClock clock)
    : IRequestHandler<RegisterPaymentCommand, Result<PaymentView>>
{
    public async Task<Result<PaymentView>> Handle(RegisterPaymentCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is not { } condominiumId)
        {
            return Result.Failure<PaymentView>(FinanceErrors.CondominiumNotFound);
        }

        if (await payments.FindChargeAsync(request.ChargeId, cancellationToken) is not { } charge)
        {
            return Result.Failure<PaymentView>(PaymentErrors.ChargeNotFound);
        }

        Result<Payment> registered = Payment.Register(
            condominiumId,
            charge.ChargeId,
            charge.Month,
            request.Amount,
            request.ReceivedOn,
            request.Method,
            request.Note,
            request.ActorUserId,
            clock.UtcNow,
            FinanceCalendar.Today(clock.UtcNow));

        if (registered.IsFailure)
        {
            return Result.Failure<PaymentView>(registered.Error);
        }

        Result added = await payments.AddAsync(registered.Value, charge.Amount, cancellationToken);

        return added.IsFailure ? Result.Failure<PaymentView>(added.Error) : Result.Success(PaymentViews.Of(registered.Value));
    }
}
