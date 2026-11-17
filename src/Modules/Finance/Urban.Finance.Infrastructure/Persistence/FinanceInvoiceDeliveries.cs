using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain.Invoices;
using Urban.Finance.Domain.Months;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>Os envios de fatura no PostgreSQL, sempre dentro do condomínio da requisição (ou da tarefa, que o escolhe antes).</summary>
public sealed class FinanceInvoiceDeliveries(FinanceDbContext db) : IInvoiceDeliveries
{
    public async Task<ClosingCharge?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken) =>
        await db.UnitCharges
            .AsNoTracking()
            .Where(charge => charge.Id == chargeId)
            .Join(
                db.MonthClosings,
                charge => charge.ClosingId,
                closing => closing.Id,
                (charge, closing) => new ClosingCharge(charge.Id, charge.UnitId, charge.Block, charge.Number, closing.Month, closing.DueDate, charge.Amount))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ClosingCharge>?> ListChargesAsync(YearMonth month, CancellationToken cancellationToken)
    {
        var closing = await db.MonthClosings
            .AsNoTracking()
            .Where(candidate => candidate.Month == month)
            .Select(candidate => new { candidate.Id, candidate.DueDate })
            .SingleOrDefaultAsync(cancellationToken);

        if (closing is null)
        {
            return null;
        }

        return await db.UnitCharges
            .AsNoTracking()
            .Where(charge => charge.ClosingId == closing.Id)
            .OrderBy(charge => charge.Position)
            .Select(charge => new ClosingCharge(charge.Id, charge.UnitId, charge.Block, charge.Number, month, closing.DueDate, charge.Amount))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvoiceDelivery?> FindAsync(Guid chargeId, CancellationToken cancellationToken) =>
        await db.InvoiceDeliveries.AsNoTracking().SingleOrDefaultAsync(delivery => delivery.ChargeId == chargeId, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, InvoiceDelivery>> ListAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken cancellationToken)
    {
        List<InvoiceDelivery> found = await db.InvoiceDeliveries
            .AsNoTracking()
            .Where(delivery => chargeIds.Contains(delivery.ChargeId))
            .ToListAsync(cancellationToken);

        return found.ToDictionary(delivery => delivery.ChargeId);
    }

    public async Task<Result> AddAsync(IReadOnlyCollection<InvoiceDelivery> deliveries, CancellationToken cancellationToken)
    {
        db.InvoiceDeliveries.AddRange(deliveries);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, InvoiceDeliveryConfiguration.ChargeIndex))
        {
            // Outro pedido gravou o envio desta cobrança no meio-tempo: o índice único é quem decide quem passa.
            db.ChangeTracker.Clear();

            return Result.Failure(InvoiceDeliveryErrors.AlreadyQueued);
        }

        return Result.Success();
    }

    public async Task SaveAsync(InvoiceDelivery delivery, CancellationToken cancellationToken) =>
        await db.InvoiceDeliveries
            .Where(stored => stored.Id == delivery.Id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(stored => stored.Status, delivery.Status)
                    .SetProperty(stored => stored.Attempts, delivery.Attempts)
                    .SetProperty(stored => stored.RequestedBy, delivery.RequestedBy)
                    .SetProperty(stored => stored.RequestedAt, delivery.RequestedAt)
                    .SetProperty(stored => stored.LastAttemptAt, delivery.LastAttemptAt)
                    .SetProperty(stored => stored.SentAt, delivery.SentAt)
                    .SetProperty(stored => stored.FailureReason, delivery.FailureReason),
                cancellationToken);
}
