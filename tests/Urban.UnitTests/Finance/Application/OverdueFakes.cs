using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Payments;

namespace Urban.UnitTests.Finance.Application;

/// <summary>
/// As cobranças em atraso vêm dos fechamentos e dos pagamentos em memória do kit, como no banco: meses fechados que venceram antes
/// de hoje e ainda têm saldo, com o pago sem os estornados.
/// </summary>
internal sealed class FakeOverdueCharges(FakeMonthClosings closings, FakePayments payments) : IOverdueCharges
{
    public int Calls { get; private set; }

    public Task<IReadOnlyList<OverdueRow>> ListAsync(DateOnly today, CancellationToken cancellationToken)
    {
        Calls++;

        IReadOnlyList<OverdueRow> rows =
        [
            .. closings.All
                .Where(closing => closing.DueDate < today)
                .SelectMany(closing => closing.Charges.Select(charge => new OverdueRow(
                    charge.Id,
                    charge.UnitId,
                    charge.Block,
                    charge.Number,
                    charge.Position,
                    closing.Month,
                    closing.DueDate,
                    charge.Amount,
                    ChargeAccount.Paid(payments.All.Where(payment => payment.ChargeId == charge.Id)))))
                .Where(row => row.Amount > row.Paid),
        ];

        return Task.FromResult(rows);
    }
}
