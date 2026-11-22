using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Urban.Operations.Infrastructure.Persistence;

internal static class PostgresErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>Gravar uma linha filha cujo pai não existe (mais): o PostgreSQL responde foreign_key_violation.</summary>
    public static bool IsMissingParentViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };
}
