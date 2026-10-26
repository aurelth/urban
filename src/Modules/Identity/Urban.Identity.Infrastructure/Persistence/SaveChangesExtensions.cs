using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Urban.Identity.Infrastructure.Persistence;

internal static class SaveChangesExtensions
{
    /// <summary>
    /// Grava tudo ou nada. Conflito (valor único repetido ou linha alterada por outra requisição) vira <c>false</c> e as
    /// mudanças rastreadas são descartadas; qualquer outra falha sobe.
    /// </summary>
    public static async Task<bool> TrySaveAsync(this DbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsConflict(ex))
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    private static bool IsConflict(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}