using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.ImageNs.Application;

public sealed record ImageProcessResult(int Done, int Failed, int Retrying);

public sealed class ImageWorkService(
    MiniCcDbContext db,
    IImageArticleRows articleRows,
    LegacyImageUrls legacy,
    IImageDownloader downloader,
    IImageStore store,
    ILogger<ImageWorkService> logger)
{
    // Hold the slot through the upload so a slow store cannot accumulate downloaded byte arrays.
    private static readonly SemaphoreSlim Transfers = new(4);
    private IQueryable<Article> Articles => db.Articles.IgnoreQueryFilters();

    public async Task<int> BackfillBatchAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = await Articles.Where(a => a.Images == null).OrderBy(a => a.Id)
            .Select(a => a.Id).Take(50).ToListAsync(cancellationToken);
        if (ids.Count == 0) return 0;

        var updated = 0;
        foreach (var id in ids)
        {
            updated += await articleRows.UpdateLockedAsync(id, article =>
            {
                if (article.Images != null) return 0;
                legacy.Backfill(article, now);
                return 1;
            }, cancellationToken);
        }

        var remaining = await Articles.CountAsync(a => a.Images == null, cancellationToken);
        logger.LogInformation("已回填 {Backfilled} 篇，剩余 {Remaining} 篇", updated, remaining);
        // Rows handled by another worker still count as a scanned batch.
        return ids.Count;
    }

    public async Task<ImageProcessResult> ProcessAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var snapshot = await Articles.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new { a.Url.Value, a.Images }).SingleOrDefaultAsync(cancellationToken);
        if (snapshot == null) return new(0, 0, 0);

        var pending = snapshot.Images?.Where(pair =>
            pair.Value.State == ArticleImageState.Pending && pair.Value.NextAttemptAt <= now) ?? [];
        var results = await Task.WhenAll(pending.Select(async pair =>
            (Url: pair.Key, Previous: pair.Value,
             Result: await TransferAsync(pair.Key, snapshot.Value, pair.Value, now, cancellationToken))));

        var done = 0;
        var failed = 0;
        var retrying = 0;
        await articleRows.UpdateLockedAsync(id, article =>
        {
            foreach (var (url, previous, result) in results)
            {
                // A resave or another worker may have updated this entry while the download ran.
                if (article.Images?.GetValueOrDefault(url) != previous || !article.ApplyImageResult(url, result))
                    continue;

                switch (result.State)
                {
                    case ArticleImageState.Done: done++; break;
                    case ArticleImageState.Failed: failed++; break;
                    case ArticleImageState.Pending: retrying++; break;
                }
            }

            article.RecomputeImagesDue();
            return 1;
        }, cancellationToken);

        return new(done, failed, retrying);
    }

    private async Task<ArticleImage> TransferAsync(string url, string articleUrl, ArticleImage previous,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await Transfers.WaitAsync(cancellationToken);
        try
        {
            var download = await downloader.DownloadAsync(url, articleUrl, cancellationToken);
            if (download.Outcome == DownloadOutcome.Success)
            {
                var hash = Convert.ToHexStringLower(SHA256.HashData(download.Content!));
                var ext = download.Ext!;
                try
                {
                    await store.PutAsync(ImageKeys.For(hash, ext), download.Content!, ImageKeys.ContentType(ext), cancellationToken);
                    return new(ArticleImageState.Done, Hash: hash, Ext: ext);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Unable to store image {Hash}.{Extension}", hash, ext);
                    download = new(DownloadOutcome.Transient, Reason: "storage");
                }
            }

            var attempts = previous.Attempts + 1;
            var next = download.Outcome == DownloadOutcome.Transient
                ? ImageRetry.NextAttempt(attempts, now, download.RetryAfter)
                : null;
            return next.HasValue
                ? new(ArticleImageState.Pending, Attempts: attempts, NextAttemptAt: next, Reason: download.Reason)
                : new(ArticleImageState.Failed, Reason: download.Reason);
        }
        finally
        {
            Transfers.Release();
        }
    }
}
