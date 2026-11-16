using Microsoft.EntityFrameworkCore;
using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Payments;
using Urban.Finance.Infrastructure.Persistence.Configurations;
using Urban.SharedKernel;

namespace Urban.Finance.Infrastructure.Persistence;

/// <summary>Os dados para pagamento no PostgreSQL: uma linha por condomínio, sempre a do tenant da requisição.</summary>
public sealed class PaymentInstructionsStore(FinanceDbContext db) : IPaymentInstructionsStore
{
    public async Task<PaymentInstructions?> FindAsync(CancellationToken cancellationToken) =>
        await db.PaymentInstructions.SingleOrDefaultAsync(cancellationToken);

    public async Task<Result> SaveAsync(PaymentInstructions instructions, CancellationToken cancellationToken)
    {
        if (db.Entry(instructions).State == EntityState.Detached)
        {
            db.PaymentInstructions.Add(instructions);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException ex) when (PostgresErrors.IsUniqueViolation(ex, PaymentInstructionsConfiguration.PrimaryKey))
        {
            db.ChangeTracker.Clear();

            return Result.Failure(FinanceErrors.ConcurrencyConflict);
        }
    }
}
