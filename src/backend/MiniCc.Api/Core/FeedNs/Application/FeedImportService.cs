using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.FeedNs.Application;

public record ImportResult(int Imported, int Skipped);
public class FeedImportService(MiniCcDbContext db, IUnitOfWork unitOfWork)
{
    public async Task<ImportResult> ImportAsync(Guid feedId, IReadOnlyList<ParsedEntry> entries, bool isInitial, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var feed = await db.Feeds.FromSqlInterpolated($"SELECT * FROM \"Feeds\" WHERE \"Id\" = {feedId} FOR UPDATE").SingleAsync(cancellationToken);
            await db.Entry(feed).Collection(f => f.Tags).LoadAsync(cancellationToken);
            var result = await ImportLockedAsync(feed, entries, isInitial, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); await unitOfWork.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch { await unitOfWork.RollbackTransactionAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }

    // The caller holds the feed row lock until metadata and imports commit together.
    public async Task<ImportResult> ImportLockedAsync(Feed feed, IReadOnlyList<ParsedEntry> entries, bool isInitial, CancellationToken cancellationToken)
    {
        if (feed.Status != FeedStatus.Active) return new(0, entries.Count);
        await unitOfWork.LockArticleCreationAsync(cancellationToken);
        var imported = 0; var skipped = 0; var newCount = 0;
        var ordered = isInitial ? entries.OrderByDescending(e => e.PublishedAt).ToList() : entries;
        foreach (var item in ordered)
        {
            var keyHash = FeedIdentity.Hash(item.EntryKey);
            var existing = db.FeedEntries.Local.FirstOrDefault(e => e.FeedId == feed.Id && e.EntryKeyHash == keyHash)
                ?? await db.FeedEntries.SingleOrDefaultAsync(e => e.FeedId == feed.Id && e.EntryKeyHash == keyHash, cancellationToken);
            if (existing != null)
            {
                if (existing.EntryKey != item.EntryKey) throw new FeedException("订阅条目标识冲突");
                existing.LastSeenAt = DateTimeOffset.UtcNow;
                var inTrash = existing.ArticleId.HasValue && await db.Articles.IgnoreQueryFilters()
                    .AnyAsync(a => a.Id == existing.ArticleId && a.DeletedAt != null, cancellationToken);
                if (!existing.Dismissed && !inTrash) existing.ContentHash = item.ContentHash;
                skipped++; continue;
            }
            var entry = FeedEntry.Create(feed.Id, item.EntryKey, item.Url, item.ContentHash);
            if (isInitial && newCount++ >= 20) { entry.Dismissed = true; skipped++; }
            else
            {
                var normalized = UrlNormalizer.Normalize(item.Url);
                var article = db.Articles.Local.FirstOrDefault(a => a.NormalizedUrl == normalized)
                    ?? await db.Articles.IgnoreQueryFilters().Where(a => a.NormalizedUrl == normalized).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).FirstOrDefaultAsync(cancellationToken);
                if (article == null)
                {
                    article = Article.Create(Url.Create(item.Url), FeedText.Limit(item.Title, 500), FeedText.Limit(item.Author, 200),
                        Content.Create(item.OriginalContent.Length == 0 ? " " : item.OriginalContent, item.ReadableContent, item.Length), item.Summary, item.ImageUrl);
                    article.SetFeedContent(item.ContentKind, item.PublishedAt);
                    foreach (var tag in feed.Tags) article.AddTag(tag);
                    db.Articles.Add(article); imported++;
                }
                else skipped++;
                entry.ArticleId = article.Id;
            }
            db.FeedEntries.Add(entry);
        }
        return new(imported, skipped);
    }
}
