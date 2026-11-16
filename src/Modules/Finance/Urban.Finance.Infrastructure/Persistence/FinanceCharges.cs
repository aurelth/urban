using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Closing;
using Urban.Finance.Domain.Months;

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
            .Select(candidate => new { candidate.Month, candidate.DueDate, candidate.ClosedAt, candidate.BasisKind })
            .SingleAsync(cancellationToken);

        decimal totalWeight = await db.UnitCharges
            .Where(candidate => candidate.ClosingId == charge.ClosingId)
            .SumAsync(candidate => candidate.Weight, cancellationToken);

        // O total de cada linha no fechamento inteiro, somado no banco (nunca uma consulta por cobrança). A ordem é a do recibo.
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
            [.. totals.OrderBy(total => total.Position).ThenBy(total => total.Kind).ThenBy(total => total.Label, StringComparer.Ordinal).Select(total => new ClosingLineTotal(total.Kind, total.Label, total.Amount))]);
    }

    public async Task<IReadOnlyList<EarlierCharge>> ListEarlierOpenAsync(Guid unitId, YearMonth before, int limit, CancellationToken cancellationToken)
    {
        // Só quem ainda deve vem do banco, com os pagamentos (sem os estornados) somados na mesma consulta. O recorte por mês é em
        // memória: a competência é um texto no banco e o operador < do tipo não vira SQL; uma unidade deve poucas cobranças.
        var owing = await (
            from charge in db.UnitCharges.AsNoTracking()
            join closing in db.MonthClosings on charge.ClosingId equals closing.Id
            where charge.UnitId == unitId
            let paid = db.Payments.Where(payment => payment.ChargeId == charge.Id && payment.ReversedAt == null).Sum(payment => (decimal?)payment.Amount) ?? 0m
            where charge.Amount > paid
            select new { charge.Id, closing.Month, closing.DueDate, charge.Amount, Paid = paid })
            .ToListAsync(cancellationToken);

        return [.. owing
            .Where(charge => charge.Month < before)
            .OrderBy(charge => charge.Month)
            .Take(limit)
            .Select(charge => new EarlierCharge(charge.Id, charge.Month, charge.DueDate, charge.Amount, charge.Paid))];
    }
}
