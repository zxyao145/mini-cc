using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.ImageNs.Application;

public sealed record CollectResult(bool Skipped, int Deleted, int Repaired);

public sealed class ImageCollector(
    MiniCcDbContext db,
    IImageArticleRows articleRows,
    IImageStore store,
    IOptions<ImageOptions> options,
    ILogger<ImageCollector> logger)
{
    public static readonly TimeSpan Grace = TimeSpan.FromHours(24);

    private const int BatchSize = 200;

    public async Task<CollectResult> CollectAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (options.Value.Mode != ImageMode.Store)
            return new(Skipped: true, Deleted: 0, Repaired: 0);

        if (await db.Articles.IgnoreQueryFilters().AnyAsync(article => article.Images == null, ct))
            return new(Skipped: true, Deleted: 0, Repaired: 0);

        var referencedHashes = await ReadReferencedHashesAsync(ct);
        if (referencedHashes == null)
            return new(Skipped: true, Deleted: 0, Repaired: 0);

        var storedImages = new List<StoredImage>();
        try
        {
            await foreach (var image in store.ListAsync(ct))
            {
                if (ImageKeys.TryParseKey(image.Key, out _, out _))
                    storedImages.Add(image);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Skipping image collection because stored images could not be listed.");
            return new(Skipped: true, Deleted: 0, Repaired: 0);
        }

        var cutoff = now - Grace;
        var storedHashes = new HashSet<string>(StringComparer.Ordinal);
        var deleted = 0;
        foreach (var image in storedImages)
        {
            ImageKeys.TryParseKey(image.Key, out var hash, out _);
            storedHashes.Add(hash);

            if (referencedHashes.Contains(hash) || image.LastModified >= cutoff)
                continue;

            var lastModified = await store.GetLastModifiedAsync(image.Key, ct);
            if (lastModified is null || lastModified >= cutoff)
                continue;

            await store.DeleteAsync(image.Key, ct);
            deleted++;
        }

        var missingHashes = referencedHashes.Except(storedHashes, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var repaired = missingHashes.Count == 0
            ? 0
            : await RepairMissingImagesAsync(missingHashes, now, ct);

        return new(Skipped: false, Deleted: deleted, Repaired: repaired);
    }

    private async Task<HashSet<string>?> ReadReferencedHashesAsync(CancellationToken ct)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var (_, images) in ReadArticleImagesAsync(ct))
        {
            if (images == null)
                return null;

            foreach (var image in images.Values)
            {
                if (image is { State: ArticleImageState.Done, Hash: { } hash })
                    hashes.Add(hash);
            }
        }

        return hashes;
    }

    private async Task<int> RepairMissingImagesAsync(
        IReadOnlySet<string> missingHashes,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var repaired = 0;
        await foreach (var (id, images) in ReadArticleImagesAsync(ct))
        {
            if (images?.Values.Any(image =>
                    image is { State: ArticleImageState.Done, Hash: { } hash } && missingHashes.Contains(hash)) == true)
            {
                repaired += await articleRows.UpdateLockedAsync(id,
                    article => article.ResetMissingImages(missingHashes, now), ct);
            }
        }

        return repaired;
    }

    private async IAsyncEnumerable<(Guid Id, Dictionary<string, ArticleImage>? Images)> ReadArticleImagesAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        Guid? after = null;
        while (true)
        {
            var query = db.Articles.IgnoreQueryFilters().AsNoTracking();
            if (after.HasValue)
                query = query.Where(article => article.Id.CompareTo(after.Value) > 0);

            var articles = await query
                .OrderBy(article => article.Id)
                .Select(article => new { article.Id, article.Images })
                .Take(BatchSize)
                .ToListAsync(ct);
            if (articles.Count == 0)
                yield break;

            foreach (var article in articles)
                yield return (article.Id, article.Images);

            after = articles[^1].Id;
        }
    }
}
