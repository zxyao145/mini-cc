using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Application.Services;
using MiniCc.Api.Core.FeedNs.Application;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class FeedImportPostgresTests(PostgresFixture fixture)
{
    private static ParsedEntry Entry(string key, string? url = null) => new(key, url ?? $"https://example.com/{Guid.NewGuid()}", "Entry", "Author", "original", "<p>summary</p>", "summary", "", 7, ContentKind.SummaryOnly, DateTimeOffset.UtcNow);
    private async Task<Feed> CreateFeed()
    {
        var feed = Feed.Create($"https://example.com/{Guid.NewGuid()}/feed", "Feed", null, FeedFormat.Rss);
        await using var db = fixture.CreateContext(); db.Feeds.Add(feed); await db.SaveChangesAsync(); return feed;
    }
    [Fact]
    public async Task InitialLimit_IsPermanent_UpdatesNeverOverwrite_AndPurgeDismisses()
    {
        var feed = await CreateFeed(); var entries = Enumerable.Range(0, 23).Select(i => Entry(i.ToString())).ToList();
        await using var db = fixture.CreateContext(); var uow = new UnitOfWork(db); var importer = new FeedImportService(db, uow);
        (await importer.ImportAsync(feed.Id, entries, true, default)).Imported.Should().Be(20);
        (await importer.ImportAsync(feed.Id, entries, false, default)).Imported.Should().Be(0);
        (await db.FeedEntries.CountAsync(x => x.FeedId == feed.Id && x.Dismissed)).Should().Be(3);
        var linked = await db.FeedEntries.FirstAsync(x => x.FeedId == feed.Id && x.ArticleId != null);
        var article = await db.Articles.SingleAsync(x => x.Id == linked.ArticleId); var body = article.ReadableContent;
        await importer.ImportAsync(feed.Id, entries.Select(e => e with { OriginalContent = "changed", ReadableContent = "changed" }).ToList(), false, default);
        (await db.Articles.AsNoTracking().SingleAsync(x => x.Id == article.Id)).ReadableContent.Should().Be(body);
        article.MoveToTrash(DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        var previousHash = linked.ContentHash;
        await importer.ImportAsync(feed.Id, entries, false, default);
        linked.ContentHash.Should().Be(previousHash, "trashed entries stop at the skip branch before updating hashes");
        await new ArticlePurger(db, uow).PurgeAsync(article.Id, null, default);
        db.ChangeTracker.Clear(); (await db.FeedEntries.SingleAsync(x => x.Id == linked.Id)).Dismissed.Should().BeTrue();
        (await importer.ImportAsync(feed.Id, entries, false, default)).Imported.Should().Be(0);
    }
    [Fact]
    public async Task ConcurrentFeeds_ShareOneArticle_AndRetainTrash()
    {
        var one = await CreateFeed(); var two = await CreateFeed(); var entry = Entry("shared");
        async Task Import(Guid id) { await using var db = fixture.CreateContext(); await new FeedImportService(db, new UnitOfWork(db)).ImportAsync(id, [entry], true, default); }
        await Task.WhenAll(Import(one.Id), Import(two.Id));
        await using var verify = fixture.CreateContext();
        (await verify.Articles.CountAsync(a => a.NormalizedUrl == entry.Url)).Should().Be(1);
        var article = await verify.Articles.SingleAsync(a => a.NormalizedUrl == entry.Url); article.MoveToTrash(DateTimeOffset.UtcNow); await verify.SaveChangesAsync();
        var three = await CreateFeed(); await Import(three.Id);
        (await verify.Articles.CountAsync(a => a.NormalizedUrl == entry.Url)).Should().Be(0);
        (await verify.FeedEntries.CountAsync(e => e.ArticleId == article.Id)).Should().Be(3);
    }
    [Fact]
    public async Task ConcurrentSameFeed_DeduplicatesWithoutAbortingTransaction()
    {
        var feed = await CreateFeed(); var entries = new[] { Entry("a"), Entry("b") };
        async Task Import() { await using var db = fixture.CreateContext(); await new FeedImportService(db, new UnitOfWork(db)).ImportAsync(feed.Id, entries, false, default); }
        await Task.WhenAll(Import(), Import());
        await using var verify = fixture.CreateContext(); (await verify.FeedEntries.CountAsync(x => x.FeedId == feed.Id)).Should().Be(2);
    }
    [Fact]
    public async Task LongIdentity_RoundTripsAndRemainsUnique()
    {
        var url = "https://example.com/" + string.Concat(Enumerable.Range(0, 800).Select(i => (char)(0x4e00+i)));
        var feed = Feed.Create(url, "Long", null, FeedFormat.Rss);
        await using var db = fixture.CreateContext(); db.Feeds.Add(feed); await db.SaveChangesAsync();
        var key = string.Concat(Enumerable.Range(0, 1800).Select(i => (char)(0x4e00+i)));
        var entry = Entry(key); var importer = new FeedImportService(db, new UnitOfWork(db));
        await importer.ImportAsync(feed.Id, [entry], true, default); await importer.ImportAsync(feed.Id, [entry], false, default);
        (await db.FeedEntries.SingleAsync(x => x.FeedId == feed.Id)).EntryKey.Should().Be(key);
        (await db.Feeds.SingleAsync(x => x.Id == feed.Id)).FeedUrl.Length.Should().BeGreaterThan(2000);
    }
}
