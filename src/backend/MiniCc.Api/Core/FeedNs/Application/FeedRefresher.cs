using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.FeedNs.Application;

public record RefreshResult(int Imported, int Skipped, string Status, string? LastError);
public class FeedRefresher(MiniCcDbContext db, IUnitOfWork unitOfWork, FeedFetcher fetcher, FeedParser parser, FeedImportService importer)
{
    public static int BackoffMinutes(int failures) => (int)Math.Min(30 * Math.Pow(2, Math.Min(10, Math.Max(0, failures - 1))), 1440);
    public async Task<Guid?> ClaimAsync(Guid id, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid();
        var count = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Feeds\" SET \"LeaseUntil\" = now() + interval '5 minutes', \"LeaseToken\" = {token} WHERE \"Id\" = {id} AND \"Status\" = 0 AND (\"LeaseUntil\" IS NULL OR \"LeaseUntil\" < now())", cancellationToken);
        return count == 1 ? token : null;
    }
    public async Task<List<Guid>> ClaimDueAsync(Guid token, CancellationToken cancellationToken) => await db.Database.SqlQuery<Guid>($"""
        UPDATE "Feeds" SET "LeaseUntil" = now() + interval '5 minutes', "LeaseToken" = {token}
        WHERE "Id" IN (SELECT "Id" FROM "Feeds" WHERE "Status" = 0 AND "NextFetchAt" <= now()
          AND ("LeaseUntil" IS NULL OR "LeaseUntil" < now()) ORDER BY "NextFetchAt" LIMIT 4 FOR UPDATE SKIP LOCKED)
        RETURNING "Id" AS "Value"
        """).ToListAsync(cancellationToken);

    public async Task<RefreshResult> RefreshAsync(Guid id, Guid token, CancellationToken cancellationToken)
    {
        var snapshot = await db.Feeds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (snapshot == null || snapshot.LeaseToken != token || snapshot.Status != FeedStatus.Active) return new(0, 0, "skipped", null);
        FeedFetch? fetched = null; ParsedFeed? parsed = null; string? error = null;
        try
        {
            fetched = await fetcher.FetchAsync(snapshot.FeedUrl, snapshot.ETag, snapshot.LastModified, cancellationToken);
            if (fetched.StatusCode == 200) parsed = parser.Parse(fetched.Body ?? "", fetched.FeedUrl);
            else if (fetched.StatusCode != 304) error = $"订阅源返回 HTTP {fetched.StatusCode}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { await ReleaseAsync(id, token); throw; }
        catch (Exception ex) { error = "更新失败：" + ex.Message; }
        try { return await FinishAsync(id, token, fetched, parsed, error, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { await ReleaseAsync(id, token); throw; }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            return await FinishAsync(id, token, fetched, null, "导入失败：" + ex.Message, cancellationToken);
        }
    }
    private Task<int> ReleaseAsync(Guid id, Guid token) => db.Feeds.Where(f => f.Id == id && f.LeaseToken == token)
        .ExecuteUpdateAsync(s => s.SetProperty(f => f.LeaseToken, (Guid?)null).SetProperty(f => f.LeaseUntil, (DateTimeOffset?)null));

    private async Task<RefreshResult> FinishAsync(Guid id, Guid token, FeedFetch? fetched, ParsedFeed? parsed, string? error, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Ignore previously tracked snapshots: user pause/unsubscribe and a reclaimed lease are authoritative.
            var stale = db.Feeds.Local.FirstOrDefault(f => f.Id == id); if (stale != null) db.Entry(stale).State = EntityState.Detached;
            var feed = await db.Feeds.FromSqlInterpolated($"SELECT * FROM \"Feeds\" WHERE \"Id\" = {id} FOR UPDATE").SingleAsync(cancellationToken);
            if (feed.LeaseToken != token || feed.LeaseUntil <= DateTimeOffset.UtcNow || feed.Status != FeedStatus.Active)
            { await unitOfWork.RollbackTransactionAsync(cancellationToken); return new(0, 0, "skipped", null); }
            var counts = new ImportResult(0, 0);
            if (error == null)
            {
                if (parsed != null)
                {
                    await db.Entry(feed).Collection(f => f.Tags).LoadAsync(cancellationToken);
                    counts = await importer.ImportLockedAsync(feed, parsed.Items, false, cancellationToken);
                    counts = counts with { Skipped = counts.Skipped + parsed.Skipped };
                    feed.OriginalTitle = FeedText.Limit(parsed.Title, 500); feed.SiteUrl = parsed.SiteUrl; feed.Format = parsed.Format; feed.MinIntervalMinutes = parsed.MinIntervalMinutes;
                    feed.ETag = fetched!.ETag; feed.LastModified = fetched.LastModified;
                }
                if (fetched?.StatusCode == 304)
                {
                    feed.ETag = fetched.ETag ?? feed.ETag;
                    feed.LastModified = fetched.LastModified ?? feed.LastModified;
                }
                feed.LastSuccessAt = DateTimeOffset.UtcNow; feed.ConsecutiveFailures = 0; feed.LastError = null;
                feed.NextFetchAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(30, feed.MinIntervalMinutes));
            }
            else
            {
                feed.ConsecutiveFailures++; feed.LastError = FeedText.Limit(error, 1000);
                feed.NextFetchAt = DateTimeOffset.UtcNow.AddMinutes(BackoffMinutes(feed.ConsecutiveFailures));
                if (fetched?.StatusCode is 429 or 503 && fetched.RetryAfter > feed.NextFetchAt) feed.NextFetchAt = fetched.RetryAfter.Value;
            }
            feed.LastFetchedAt = DateTimeOffset.UtcNow; feed.LeaseUntil = null; feed.LeaseToken = null;
            await unitOfWork.SaveChangesAsync(cancellationToken); await unitOfWork.CommitTransactionAsync(cancellationToken);
            return new(counts.Imported, counts.Skipped, error == null ? "ok" : "failing", feed.LastError);
        }
        catch { await unitOfWork.RollbackTransactionAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
}

public class FeedRefreshWorker(IServiceScopeFactory scopes, ILogger<FeedRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var token = Guid.NewGuid();
                using var scope = scopes.CreateScope();
                var ids = await scope.ServiceProvider.GetRequiredService<FeedRefresher>().ClaimDueAsync(token, stoppingToken);
                await Task.WhenAll(ids.Select(async id =>
                {
                    try { using var feedScope = scopes.CreateScope(); await feedScope.ServiceProvider.GetRequiredService<FeedRefresher>().RefreshAsync(id, token, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                    catch (Exception ex) { logger.LogError(ex, "Feed refresh failed for {FeedId}", id); }
                }));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Unable to claim feed refresh tasks"); }
        }
    }
}
