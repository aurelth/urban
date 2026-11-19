using MediatR;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Finance.Application.Delinquency;

public sealed class ListDelinquencyQueryHandler(IOverdueCharges overdue, ITenantContext tenant, IClock clock)
    : IRequestHandler<ListDelinquencyQuery, Result<DelinquencyView>>
{
    public const int MaxPageSize = 200;

    public async Task<Result<DelinquencyView>> Handle(ListDelinquencyQuery request, CancellationToken cancellationToken)
    {
        if (tenant.CondominiumId is null)
        {
            return Result.Failure<DelinquencyView>(FinanceErrors.CondominiumNotFound);
        }

        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > MaxPageSize)
        {
            return Result.Failure<DelinquencyView>(DelinquencyErrors.PageInvalid);
        }

        DateOnly today = FinanceCalendar.Today(clock.UtcNow);
        IReadOnlyList<OverdueRow> rows = await overdue.ListAsync(today, cancellationToken);

        // A porta só pré-filtra: a regra de "atrasada" é uma só, a do Domain.
        List<Debtor> debtors =
        [
            .. rows
                .Where(row => ChargeAccount.StatusOf(row.Amount, row.Paid, row.DueDate, today) == ChargeStatus.Overdue)
                .GroupBy(row => row.UnitId)
                .Select(group => Debtor.Of(group, today))
                .OrderByDescending(debtor => debtor.View.Balance)
                .ThenBy(debtor => debtor.View.OldestDueDate)
                .ThenBy(debtor => debtor.Position),
        ];

        return Result.Success(new DelinquencyView(
            today,
            debtors.Sum(debtor => debtor.View.Balance),
            debtors.Count,
            request.Page,
            request.PageSize,
            [.. debtors.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).Select(debtor => debtor.View)]));
    }

    /// <summary>O devedor pronto e o lugar da unidade na ordem natural, só para desempatar a lista.</summary>
    private sealed record Debtor(DebtorView View, int Position)
    {
        public static Debtor Of(IGrouping<Guid, OverdueRow> unit, DateOnly today)
        {
            // Bloco e número são cópias de quando cada mês fechou: vale o do mês mais recente.
            OverdueRow latest = unit.MaxBy(row => row.Month)!;
            List<OverdueChargeView> charges =
            [
                .. unit
                    .OrderBy(row => row.DueDate)
                    .ThenBy(row => row.Month)
                    .Select(row => new OverdueChargeView(
                        row.ChargeId,
                        row.Month.ToString(),
                        row.DueDate,
                        row.Amount,
                        row.Paid,
                        ChargeAccount.Balance(row.Amount, row.Paid),
                        today.DayNumber - row.DueDate.DayNumber)),
            ];

            return new Debtor(
                new DebtorView(
                    unit.Key,
                    latest.Block,
                    latest.Number,
                    charges.Sum(charge => charge.Balance),
                    charges.Count,
                    charges[0].DueDate,
                    charges[0].DaysOverdue,
                    charges),
                latest.Position);
        }
    }
}
