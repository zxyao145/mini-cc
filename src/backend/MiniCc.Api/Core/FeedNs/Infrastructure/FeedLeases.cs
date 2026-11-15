using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.FeedNs.Infrastructure;

public static class FeedLeases
{
    public static Task<int> ClaimAsync(MiniCcDbContext db, Guid id, Guid token, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite())
            return db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Feeds\" SET \"LeaseUntil\" = now() + interval '5 minutes', \"LeaseToken\" = {token} WHERE \"Id\" = {id} AND \"Status\" = 0 AND (\"LeaseUntil\" IS NULL OR \"LeaseUntil\" < now())", cancellationToken);

        var (nowTicks, untilTicks) = GetSqliteLeaseTimes();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Feeds" SET "LeaseUntil" = {untilTicks}, "LeaseToken" = {token}
            WHERE "Id" = {id} AND "Status" = 0 AND ("LeaseUntil" IS NULL OR "LeaseUntil" < {nowTicks})
            """, cancellationToken);
    }

    public static Task<List<Guid>> ClaimDueAsync(MiniCcDbContext db, Guid token, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite())
            return db.Database.SqlQuery<Guid>($"""
                UPDATE "Feeds" SET "LeaseUntil" = now() + interval '5 minutes', "LeaseToken" = {token}
                WHERE "Id" IN (SELECT "Id" FROM "Feeds" WHERE "Status" = 0 AND "NextFetchAt" <= now()
                  AND ("LeaseUntil" IS NULL OR "LeaseUntil" < now()) ORDER BY "NextFetchAt" LIMIT 4 FOR UPDATE SKIP LOCKED)
                RETURNING "Id" AS "Value"
                """).ToListAsync(cancellationToken);

        var (nowTicks, untilTicks) = GetSqliteLeaseTimes();
        return db.Database.SqlQuery<Guid>($"""
            UPDATE "Feeds" SET "LeaseUntil" = {untilTicks}, "LeaseToken" = {token}
            WHERE "Id" IN (SELECT "Id" FROM "Feeds" WHERE "Status" = 0 AND "NextFetchAt" <= {nowTicks}
              AND ("LeaseUntil" IS NULL OR "LeaseUntil" < {nowTicks}) ORDER BY "NextFetchAt" LIMIT 4)
            RETURNING "Id" AS "Value"
            """).ToListAsync(cancellationToken);
    }

    private static (long NowTicks, long UntilTicks) GetSqliteLeaseTimes()
    {
        var now = DateTimeOffset.UtcNow;
        return (now.UtcTicks, now.AddMinutes(5).UtcTicks);
    }
}
