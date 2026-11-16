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
            .Select(candidate => new { candidate.Month, candidate.DueDate, candidate.ClosedAt })
            .SingleAsync(cancellationToken);

        return new ChargeDetail(charge, closing.Month, closing.DueDate, closing.ClosedAt);
    }
}
