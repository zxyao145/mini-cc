using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.FeedNs.Application;

public record FeedDto(Guid Id, string Title, string OriginalTitle, string FeedUrl, string? SiteUrl, FeedFormat Format,
    string Status, string Health, DateTimeOffset? LastSuccessAt, DateTimeOffset? LastFetchedAt, string? LastError,
    int UnreadCount, int ArticleCount, List<TagDto> Tags)
{
    public static readonly Expression<Func<Feed, FeedDto>> Projection = f => new(f.Id, f.Title, f.OriginalTitle, f.FeedUrl, f.SiteUrl, f.Format,
        f.Status == FeedStatus.Paused ? "paused" : "active", f.ConsecutiveFailures > 0 ? "failing" : "ok", f.LastSuccessAt, f.LastFetchedAt, f.LastError,
        f.Entries.Where(e => e.Article != null && e.Article.ReadingStatus == ReadingStatus.Unread).Select(e => e.ArticleId).Distinct().Count(),
        f.Entries.Where(e => e.Article != null).Select(e => e.ArticleId).Distinct().Count(),
        f.Tags.Select(t => new TagDto { Id = t.Id, Name = t.Name, Color = t.Color.Value, CreatedAt = t.CreatedAt }).ToList());
}
public record FeedPreview(string FeedUrl, string Title, string? SiteUrl, FeedFormat Format, List<FeedPreviewItem> Items, Guid? ExistingFeedId);
public record FeedPreviewItem(string Title, string Url, DateTimeOffset? PublishedAt, ContentKind ContentKind);
public record CreateFeedResult(FeedDto? Feed, int Imported, Guid? ExistingFeedId = null);
public record FeedTrashResult(int Trashed, int Skipped);

public class FeedService(MiniCcDbContext db, IUnitOfWork unitOfWork, FeedFetcher fetcher, FeedParser parser, FeedImportService importer)
{
    public async Task<FeedPreview> PreviewAsync(string url, CancellationToken ct)
    {
        var (fetched, parsed) = await FetchAsync(url, ct);
        var hash = FeedIdentity.Hash(UrlNormalizer.Normalize(fetched.FeedUrl));
        var existing = await db.Feeds.AsNoTracking().SingleOrDefaultAsync(f => f.FeedUrlHash == hash, ct);
        CheckIdentity(existing, UrlNormalizer.Normalize(fetched.FeedUrl));
        return new(fetched.FeedUrl, parsed.Title, parsed.SiteUrl, parsed.Format,
            parsed.Items.Take(5).Select(e => new FeedPreviewItem(e.Title, e.Url, e.PublishedAt, e.ContentKind)).ToList(), existing?.Id);
    }
    private async Task<(FeedFetch, ParsedFeed)> FetchAsync(string url, CancellationToken ct)
    {
        var fetched = await fetcher.FetchAsync(url, null, null, ct);
        if (fetched.StatusCode != 200) throw new FeedException($"订阅源返回 HTTP {fetched.StatusCode}");
        return (fetched, parser.Parse(fetched.Body ?? "", fetched.FeedUrl));
    }
    public Task<List<FeedDto>> ListAsync(CancellationToken ct) => db.Feeds.AsNoTracking().Where(f => f.Status != FeedStatus.Unsubscribed).OrderBy(f => f.Title).Select(FeedDto.Projection).ToListAsync(ct);
    public Task<FeedDto?> GetAsync(Guid id, CancellationToken ct) => db.Feeds.AsNoTracking().Where(f => f.Id == id).Select(FeedDto.Projection).SingleOrDefaultAsync(ct);
    private static void CheckIdentity(Feed? feed, string url) { if (feed != null && feed.FeedUrl != url) throw new FeedException("订阅地址标识冲突"); }

    public async Task<CreateFeedResult> CreateAsync(string url, string title, Guid[] tagIds, CancellationToken ct)
    {
        var (fetched, parsed) = await FetchAsync(url, ct);
        var normalized = UrlNormalizer.Normalize(fetched.FeedUrl); var hash = FeedIdentity.Hash(normalized);
        await unitOfWork.BeginTransactionAsync(ct);
        Guid id; ImportResult imported;
        try
        {
            // Serialize feed identity creation separately, always before feed-row and article-creation locks.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(70453126)", ct);
            var feed = await db.Feeds.FromSqlInterpolated($"SELECT * FROM \"Feeds\" WHERE \"FeedUrlHash\" = {hash} FOR UPDATE").SingleOrDefaultAsync(ct);
            CheckIdentity(feed, normalized);
            if (feed != null && feed.Status != FeedStatus.Unsubscribed) { await unitOfWork.RollbackTransactionAsync(ct); return new(null, 0, feed.Id); }
            var tags = await db.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync(ct);
            if (tags.Count != tagIds.Distinct().Count()) throw new ArgumentException("默认标签不存在");
            if (feed == null) { feed = Feed.Create(normalized, FeedText.Limit(parsed.Title, 500), parsed.SiteUrl, parsed.Format); db.Feeds.Add(feed); }
            else { await db.Entry(feed).Collection(f => f.Tags).LoadAsync(ct); feed.Tags.Clear(); }
            feed.Title = title; feed.Status = FeedStatus.Active; feed.LeaseToken = null; feed.LeaseUntil = null;
            foreach (var tag in tags) feed.Tags.Add(tag);
            // Persist the new parent before importing, inside this same transaction.
            await unitOfWork.SaveChangesAsync(ct);
            imported = await importer.ImportLockedAsync(feed, parsed.Items, true, ct);
            feed.ETag = fetched.ETag; feed.LastModified = fetched.LastModified; feed.LastFetchedAt = feed.LastSuccessAt = DateTimeOffset.UtcNow;
            feed.LastError = null; feed.ConsecutiveFailures = 0; feed.MinIntervalMinutes = parsed.MinIntervalMinutes;
            feed.NextFetchAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(30, parsed.MinIntervalMinutes));
            await unitOfWork.SaveChangesAsync(ct); await unitOfWork.CommitTransactionAsync(ct); id = feed.Id;
        }
        catch { await unitOfWork.RollbackTransactionAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        return new(await GetAsync(id, ct), imported.Imported);
    }
    public async Task<FeedDto?> EditAsync(Guid id, string? title, Guid[]? tagIds, bool? paused, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var feed = await db.Feeds.FromSqlInterpolated($"SELECT * FROM \"Feeds\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (feed == null || feed.Status == FeedStatus.Unsubscribed) { await unitOfWork.RollbackTransactionAsync(ct); return null; }
            if (title != null) feed.Title = title;
            if (tagIds != null)
            {
                var tags = await db.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync(ct);
                if (tags.Count != tagIds.Distinct().Count()) throw new ArgumentException("默认标签不存在");
                await db.Entry(feed).Collection(f => f.Tags).LoadAsync(ct); feed.Tags.Clear(); foreach (var tag in tags) feed.Tags.Add(tag);
            }
            if (paused.HasValue)
            {
                feed.Status = paused.Value ? FeedStatus.Paused : FeedStatus.Active;
                feed.LeaseToken = null; feed.LeaseUntil = null;
                if (!paused.Value) feed.NextFetchAt = DateTimeOffset.UtcNow;
            }
            await unitOfWork.SaveChangesAsync(ct); await unitOfWork.CommitTransactionAsync(ct);
        }
        catch { await unitOfWork.RollbackTransactionAsync(CancellationToken.None); throw; }
        return await GetAsync(id, ct);
    }
    public async Task<bool> UnsubscribeAsync(Guid id, CancellationToken ct) => await db.Feeds.Where(f => f.Id == id).ExecuteUpdateAsync(s => s
        .SetProperty(f => f.Status, FeedStatus.Unsubscribed).SetProperty(f => f.LeaseToken, (Guid?)null).SetProperty(f => f.LeaseUntil, (DateTimeOffset?)null), ct) > 0;

    public async Task<FeedTrashResult?> TrashArticlesAsync(Guid id, CancellationToken ct)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Same guard as import and plugin save: provenance cannot change between eligibility and trashing.
            await unitOfWork.LockArticleCreationAsync(ct);
            if (!await db.Feeds.AnyAsync(f => f.Id == id, ct)) { await unitOfWork.RollbackTransactionAsync(ct); return null; }
            var articles = db.Articles.Where(a => a.FeedEntries.Any(e => e.FeedId == id));
            var eligible = articles.Where(a => a.SavedFromExtensionAt == null && !a.FeedEntries.Any(e => e.FeedId != id));
            var total = await articles.CountAsync(ct);
            var trashed = await eligible.ExecuteUpdateAsync(s => s.SetProperty(a => a.DeletedAt, DateTimeOffset.UtcNow), ct);
            await unitOfWork.CommitTransactionAsync(ct); return new(trashed, total - trashed);
        }
        catch { await unitOfWork.RollbackTransactionAsync(CancellationToken.None); throw; }
    }
}
