using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace MiniCc.Api.Shared.Data;

public static class DatabaseLocks
{
    public const long ArticleCreationLockKey = 70453125;
    public const long FeedIdentityLockKey = 70453126;

    // SQLite callers already hold the write lock acquired by BEGIN IMMEDIATE.
    public static IQueryable<TEntity> FromSqlForUpdate<TEntity>(this DbSet<TEntity> set, FormattableString sql)
        where TEntity : class
    {
        return set.GetService<ICurrentDbContext>().Context.Database.IsSqlite()
            ? set.FromSql(sql)
            : set.FromSql(FormattableStringFactory.Create(sql.Format + " FOR UPDATE", sql.GetArguments()));
    }

    public static Task AcquireTransactionLockAsync(this DatabaseFacade database, long key, CancellationToken cancellationToken) =>
        database.IsSqlite()
            ? Task.CompletedTask
            : database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(" + key + ")", cancellationToken);
}
