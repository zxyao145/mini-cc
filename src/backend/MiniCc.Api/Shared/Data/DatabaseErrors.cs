using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MiniCc.Api.Shared.Data;

public static class DatabaseErrors
{
    private const int SqliteConstraintUnique = 2067;

    public static bool IsUniqueViolation(DbUpdateException exception, string table, string postgresConstraint, string sqliteColumn) =>
        exception.InnerException switch
        {
            PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres =>
                postgres.TableName == table && postgres.ConstraintName == postgresConstraint,
            SqliteException { SqliteExtendedErrorCode: SqliteConstraintUnique } sqlite =>
                sqlite.Message.Contains($"{table}.{sqliteColumn}", StringComparison.Ordinal),
            _ => false
        };
}
