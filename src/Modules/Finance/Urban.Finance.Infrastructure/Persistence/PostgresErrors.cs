using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Urban.Finance.Infrastructure.Persistence;

internal static class PostgresErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres && postgres.ConstraintName == constraintName;

    public static bool IsAnyUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
