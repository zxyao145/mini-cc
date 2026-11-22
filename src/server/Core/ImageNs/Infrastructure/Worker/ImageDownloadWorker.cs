using Microsoft.Extensions.Options;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Core.ImageNs.Application;

namespace MiniCc.Api.Core.ImageNs.Infrastructure.Worker;

public sealed class ImageDownloadWorker(
    IServiceScopeFactory scopes,
    IOptions<ImageOptions> options,
    ILogger<ImageDownloadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var work = scope.ServiceProvider.GetRequiredService<ImageWorkService>();
                var now = DateTimeOffset.UtcNow;
                while (await work.BackfillBatchAsync(now, stoppingToken) > 0) { }

                if (options.Value.Mode != ImageMode.Store) continue;

                now = DateTimeOffset.UtcNow;
                var db = scope.ServiceProvider.GetRequiredService<MiniCcDbContext>();
                var ids = await ImageLeases.ClaimDueAsync(db, now, stoppingToken);
                var results = await Task.WhenAll(ids.Select(id => ProcessAsync(id, now, stoppingToken)));
                logger.LogInformation("Image download processed {Articles} articles: {Done} done, {Failed} failed, {Retrying} retrying",
                    ids.Count, results.Sum(r => r.Done), results.Sum(r => r.Failed), results.Sum(r => r.Retrying));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Image download cycle failed"); }
        }
    }

    private async Task<ImageProcessResult> ProcessAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ImageWorkService>().ProcessAsync(id, now, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Image download failed for article {ArticleId}", id);
            return new(0, 0, 0);
        }
    }
}
