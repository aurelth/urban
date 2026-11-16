using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Months;

/// <summary>
/// Reabre o mês: apaga o fechamento e as cobranças dele numa unidade atômica. Os lançamentos não mudam. Mês com algum pagamento,
/// mesmo estornado, não reabre: o histórico de recebimentos ficaria sem a cobrança a que pertence.
/// </summary>
public sealed class ReopenMonthCommandHandler(
    IExpenseEntries entries,
    IMonthClosings closings,
    IExpenseGroups groups,
    IPayments payments,
    ITenantContext tenant,
    IAtomicWork atomicWork,
    IClock clock) : IRequestHandler<ReopenMonthCommand, Result<MonthView>>
{
    public async Task<Result<MonthView>> Handle(ReopenMonthCommand request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<MonthView>(FinanceErrors.CondominiumNotFound);
        }

        Result<YearMonth> parsed = YearMonth.Parse(request.Month);

        if (parsed.IsFailure)
        {
            return Result.Failure<MonthView>(parsed.Error);
        }

        YearMonth month = parsed.Value;

        if (await closings.FindAsync(month, cancellationToken) is not { } closing)
        {
            return Result.Failure<MonthView>(ClosingErrors.NotClosed);
        }

        if (await payments.AnyForClosingAsync(closing.Id, cancellationToken))
        {
            return Result.Failure<MonthView>(ClosingErrors.HasPayments);
        }

        return await Atomic.RunAsync(
            atomicWork,
            async ct =>
            {
                Result removed = await closings.RemoveAsync(closing, ct);

                if (removed.IsFailure)
                {
                    return Result.Failure<MonthView>(removed.Error);
                }

                return Result.Success(await MonthReader.ReadAsync(entries, closings, groups, month, FinanceCalendar.Today(clock.UtcNow), ct));
            },
            cancellationToken);
    }
}
