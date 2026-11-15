using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniCc.Api.Shared.Config;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.ArticleNs.Application.Services;

public class TrashCleanup(MiniCcDbContext db, ArticlePurger purger, ILogger<TrashCleanup> logger)
{
    public async Task<int> PurgeAsync(DateTimeOffset? expiredBefore, CancellationToken cancellationToken)
    {
        var purged = 0;
        Guid? after = null;
        while (true)
        {
            var query = db.Articles.IgnoreQueryFilters().Where(a => a.DeletedAt != null);
            if (expiredBefore.HasValue) query = query.Where(a => a.DeletedAt <= expiredBefore);
            if (after.HasValue) query = query.Where(a => a.Id.CompareTo(after.Value) > 0);
            var ids = await query.OrderBy(a => a.Id).Select(a => a.Id).Take(100).ToListAsync(cancellationToken);
            if (ids.Count == 0) return purged;
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { if (await purger.PurgeAsync(id, expiredBefore, cancellationToken) == null) purged++; }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { db.ChangeTracker.Clear(); logger.LogError(ex, "Purge failed for article {ArticleId}", id); }
            }
            after = ids[^1];
            db.ChangeTracker.Clear();
        }
    }
}
public class TrashPurgeWorker(IServiceScopeFactory scopes, IOptions<LibraryOptions> options, ILogger<TrashPurgeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<TrashCleanup>().PurgeAsync(DateTimeOffset.UtcNow.AddDays(-options.Value.TrashRetentionDays), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogError(ex, "Trash retention cleanup failed"); }
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
