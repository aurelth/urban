using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>As cobranças em atraso no PostgreSQL, sempre dentro do condomínio da requisição.</summary>
public sealed class FinanceOverdueCharges(FinanceDbContext db) : IOverdueCharges
{
    public async Task<IReadOnlyList<OverdueRow>> ListAsync(DateOnly today, CancellationToken cancellationToken)
    {
        // Uma consulta só, para todos os meses: o pago é somado no banco (sem os estornados) e só voltam as cobranças vencidas
        // com saldo, então as pagas e as ainda no prazo ficam de fora. Quem decide a situação é ChargeAccount.StatusOf.
        IQueryable<OverdueRow> query =
            from charge in db.UnitCharges.AsNoTracking()
            join closing in db.MonthClosings.AsNoTracking() on charge.ClosingId equals closing.Id
            where closing.DueDate < today
            let paid = db.Payments.Where(payment => payment.ChargeId == charge.Id && payment.ReversedAt == null).Sum(payment => (decimal?)payment.Amount) ?? 0m
            where charge.Amount > paid
            select new OverdueRow(charge.Id, charge.UnitId, charge.Block, charge.Number, charge.Position, closing.Month, closing.DueDate, charge.Amount, paid);

        return await query.ToListAsync(cancellationToken);
    }
}
