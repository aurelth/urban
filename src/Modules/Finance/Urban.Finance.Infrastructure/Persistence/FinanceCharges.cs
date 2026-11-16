using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;

namespace Urban.Finance.Infrastructure.Persistence;

public sealed class FinanceCharges(FinanceDbContext db) : ICharges
{
    public async Task<ChargeDetail?> FindAsync(Guid chargeId, CancellationToken cancellationToken)
    {
        UnitCharge? charge = await db.UnitCharges
            .AsNoTracking()
            .Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == chargeId, cancellationToken);

        if (charge is null)
        {
            return null;
        }

        var closing = await db.MonthClosings
            .AsNoTracking()
            .Where(candidate => candidate.Id == charge.ClosingId)
            .Select(candidate => new { candidate.Month, candidate.DueDate, candidate.ClosedAt, candidate.BasisKind, candidate.ReserveFundPercent, candidate.ReserveFundBase })
            .SingleAsync(cancellationToken);

        decimal totalWeight = await db.UnitCharges
            .Where(candidate => candidate.ClosingId == charge.ClosingId)
            .SumAsync(candidate => candidate.Weight, cancellationToken);

        // O total de cada linha no fechamento inteiro, somado no banco (nunca uma consulta por cobrança). A ordem é a da fatura.
        var totals = await (
            from line in db.UnitChargeLines.AsNoTracking()
            join other in db.UnitCharges on line.ChargeId equals other.Id
            where other.ClosingId == charge.ClosingId
            group line by new { line.Kind, line.Label } into lines
            select new { lines.Key.Kind, lines.Key.Label, Amount = lines.Sum(line => line.Amount), Position = lines.Min(line => line.Position) })
            .ToListAsync(cancellationToken);

        return new ChargeDetail(
            charge,
            closing.Month,
            closing.DueDate,
            closing.ClosedAt,
            closing.BasisKind,
            totalWeight,
            [.. totals.OrderBy(total => total.Position).ThenBy(total => total.Kind).ThenBy(total => total.Label, StringComparer.Ordinal).Select(total => new ClosingLineTotal(total.Kind, total.Label, total.Amount))],
            closing.ReserveFundPercent,
            closing.ReserveFundBase);
    }
}
