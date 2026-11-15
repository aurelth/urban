using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Urban.Finance.Infrastructure.Persistence;

internal static class PostgresErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres && postgres.ConstraintName == constraintName;

    public static bool IsAnyUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    /// <summary>Apagar a linha pai com filha apontando para ela: o PostgreSQL responde restrict_violation (FK com Restrict) ou foreign_key_violation.</summary>
    public static bool IsReferencedRowViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.RestrictViolation or PostgresErrorCodes.ForeignKeyViolation };
}
