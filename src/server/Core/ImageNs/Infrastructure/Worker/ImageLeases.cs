using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.ImageNs.Infrastructure.Worker;

public static class ImageLeases
{
    public static Task<List<Guid>> ClaimDueAsync(MiniCcDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var until = now.AddMinutes(10);
        if (db.Database.IsSqlite())
        {
            return db.Database.SqlQuery<Guid>($"""
                UPDATE "Articles" SET "ImagesDueAt" = {until.UtcTicks}
                WHERE "Id" IN (
                    SELECT "Id" FROM "Articles" WHERE "ImagesDueAt" <= {now.UtcTicks}
                    ORDER BY "ImagesDueAt", "Id" LIMIT 10)
                RETURNING "Id" AS "Value"
                """).ToListAsync(cancellationToken);
        }

        return db.Database.SqlQuery<Guid>($"""
            UPDATE "Articles" SET "ImagesDueAt" = {until.ToUniversalTime()}
            WHERE "Id" IN (
                SELECT "Id" FROM "Articles" WHERE "ImagesDueAt" <= {now.ToUniversalTime()}
                ORDER BY "ImagesDueAt", "Id" LIMIT 10 FOR UPDATE SKIP LOCKED)
            RETURNING "Id" AS "Value"
            """).ToListAsync(cancellationToken);
    }
}
