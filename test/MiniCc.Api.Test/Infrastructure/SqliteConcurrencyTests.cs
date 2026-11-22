using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniCc.Api.Core.ArticleNs.Application.Services;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.FeedNs.Application;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public class SqliteConcurrencyTests(SqliteFixture fixture)
{
    [Fact]
    public async Task BeginTransaction_IsImmediate_SecondWriterWaitsForCommit()
    {
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext(fixture.ConnectionString + ";Default Timeout=1");
        await using var transaction = await first.Database.BeginTransactionAsync();

        Func<Task> begin = async () => { await using var blocked = await second.Database.BeginTransactionAsync(); };
        var error = await begin.Should().ThrowAsync<SqliteException>();
        error.Which.SqliteErrorCode.Should().Be(5); // SQLITE_BUSY

        await transaction.CommitAsync();
        await using var next = await second.Database.BeginTransactionAsync();
        await next.CommitAsync();
    }

    [Fact]
    public async Task LockedLookups_LoadRowsOnSqlite()
    {
        var article = await fixture.SeedArticleAsync();
        await using var db = fixture.CreateContext();
        var unitOfWork = new UnitOfWork(db);
        await unitOfWork.BeginTransactionAsync();
        await unitOfWork.LockArticleCreationAsync();

        (await unitOfWork.Articles.GetByIdForProgressUpdateAsync(article.Id))!.Id.Should().Be(article.Id);
        (await unitOfWork.Articles.GetByUrlAsync(article.NormalizedUrl))!.Id.Should().Be(article.Id);

        await unitOfWork.CommitTransactionAsync();
    }

    [Fact]
    public async Task ConcurrentExtensionSaves_SameUrl_CreateOneArticle()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var url = $"https://example.com/{Guid.NewGuid():N}";
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => SaveAsync(client, url, i)));

        responses.Should().OnlyContain(r => r.IsSuccessStatusCode);
        await AssertSingleArticleAsync(url);
    }

    [Fact]
    public async Task ConcurrentProgressSaves_SameStaleBase_OneConflict()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync();
        var responses = await Task.WhenAll(new[] { "session-a", "session-b" }.Select(sessionId =>
            client.PutAsJsonAsync($"/api/articles/{article.Id}/progress",
                new { textOffset = 10, blockIndex = 2, blockRatio = .5, percent = 42, sessionId, baseUpdatedAt = (string?)null })));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
    }

    [Fact]
    public async Task FeedLeases_ClaimDue_TwoWorkersClaimDisjointBatches()
    {
        await using var setup = fixture.CreateContext();
        var now = DateTimeOffset.UtcNow;
        await setup.Feeds.ExecuteUpdateAsync(s => s.SetProperty(f => f.NextFetchAt, now.AddDays(1)));

        var due = Enumerable.Range(0, 6).Select(_ => NewFeed(now.AddMinutes(-1))).ToList();
        var paused = NewFeed(now.AddMinutes(-1));
        paused.Status = FeedStatus.Paused;
        var leased = NewFeed(now.AddMinutes(-1));
        leased.LeaseUntil = now.AddMinutes(5);
        setup.Feeds.AddRange([.. due, paused, leased]);
        await setup.SaveChangesAsync();
        await using var one = fixture.CreateContext();
        await using var two = fixture.CreateContext();
        var a = Refresher(one, NotModified);
        var b = Refresher(two, NotModified);
        var batches = await Task.WhenAll(
            Task.Run(() => a.ClaimDueAsync(Guid.NewGuid(), default)),
            Task.Run(() => b.ClaimDueAsync(Guid.NewGuid(), default)));

        batches.Should().OnlyContain(batch => batch.Count <= 4);
        batches.SelectMany(batch => batch).Should().BeEquivalentTo(due.Select(f => f.Id));

        var expiredAt = now.AddMinutes(-1);
        await setup.Feeds.Where(f => f.Id == leased.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.LeaseUntil, expiredAt));
        (await a.ClaimDueAsync(Guid.NewGuid(), default)).Should().Equal(leased.Id);
    }

    [Fact]
    public async Task Lease_IsAtomic_AndStaleOwnerCannotClearReclaimedLease()
    {
        var feed = await SeedFeedAsync();
        await using var one = fixture.CreateContext();
        await using var two = fixture.CreateContext();
        var a = Refresher(one, NotModified);
        var b = Refresher(two, NotModified);
        var tokens = await Task.WhenAll(Task.Run(() => a.ClaimAsync(feed.Id, default)),
            Task.Run(() => b.ClaimAsync(feed.Id, default)));
        var old = tokens.OfType<Guid>().Should().ContainSingle().Subject;

        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await one.Feeds.Where(f => f.Id == feed.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.LeaseUntil, expiredAt));
        var current = await b.ClaimAsync(feed.Id, default);
        current.Should().NotBeNull().And.NotBe(old);

        (await a.RefreshAsync(feed.Id, old, default)).Status.Should().Be("skipped");
        (await one.Feeds.AsNoTracking().SingleAsync(f => f.Id == feed.Id)).LeaseToken.Should().Be(current);
    }

    [Fact]
    public async Task PurgeRacingRestore_NeverDeletesRestoredArticle()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        for (var i = 0; i < 5; i++)
        {
            var article = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
            var responses = await Task.WhenAll(
                client.PostAsJsonAsync("/api/articles/batch", new { ids = new[] { article.Id }, action = "purge" }),
                client.PostAsync($"/api/articles/{article.Id}/restore", null));

            responses[0].EnsureSuccessStatusCode();
            var batch = await responses[0].Content.ReadFromJsonAsync<JsonElement>();
            await using var db = fixture.CreateContext();
            if (responses[1].StatusCode == HttpStatusCode.OK)
            {
                (await db.Articles.AnyAsync(a => a.Id == article.Id)).Should().BeTrue();
                var failure = batch.GetProperty("failed")[0];
                failure.GetProperty("id").GetGuid().Should().Be(article.Id);
                failure.GetProperty("reason").GetString().Should().Be("notInTrash");
            }
            else
            {
                responses[1].StatusCode.Should().Be(HttpStatusCode.NotFound);
                batch.GetProperty("succeeded")[0].GetGuid().Should().Be(article.Id);
                (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == article.Id)).Should().BeFalse();
            }
        }
    }

    [Fact]
    public async Task TrashCleanup_PurgesAtExactCutoff()
    {
        var cutoff = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        var atCutoff = await fixture.SeedArticleAsync(a => a.MoveToTrash(cutoff));
        var afterCutoff = await fixture.SeedArticleAsync(a => a.MoveToTrash(cutoff.AddTicks(1)));
        await using var db = fixture.CreateContext();
        var uow = new UnitOfWork(db);
        var cleanup = new TrashCleanup(db, new ArticlePurger(db, uow), NullLogger<TrashCleanup>.Instance);

        (await cleanup.PurgeAsync(cutoff.AddTicks(-1), default)).Should().Be(0);
        (await cleanup.PurgeAsync(cutoff, default)).Should().Be(1);
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == atCutoff.Id)).Should().BeFalse();
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == afterCutoff.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task FeedRefreshDuringExtensionSaves_CompletesWithoutBusyErrors()
    {
        var feed = await SeedFeedAsync();
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        await using var db = fixture.CreateContext();
        var prefix = $"https://example.com/{Guid.NewGuid():N}";
        var items = string.Concat(Enumerable.Range(0, 20).Select(i =>
            $"<item><title>Item {i}</title><link>{prefix}/feed/{i}</link><guid>{i}</guid><description>Summary</description></item>"));
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refresher = Refresher(db, () => { reading.SetResult(); return reply.Task; });
        var token = (await refresher.ClaimAsync(feed.Id, default))!.Value;
        var refresh = refresher.RefreshAsync(feed.Id, token, default);
        await reading.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var sharedUrl = $"{prefix}/feed/0";
        var saves = Task.WhenAll(Enumerable.Range(0, 10).Select(i => SaveAsync(client, i == 0 ? sharedUrl : $"{prefix}/saved/{i}", i)));
        reply.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"<rss version='2.0'><channel><title>Feed</title><link>{prefix}</link><description>Feed</description>{items}</channel></rss>")
        });

        var result = await refresh;
        result.Status.Should().Be("ok", result.LastError);
        (result.Imported + result.Skipped).Should().Be(20);
        (await saves).Should().OnlyContain(r => r.IsSuccessStatusCode);
        await AssertSingleArticleAsync(sharedUrl);
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, string url, int index) =>
        client.PostAsJsonAsync("/api/articles/content",
            new { url, title = $"Title {index}", originalContent = $"<p>Original {index}</p>", readableContent = $"<p>Readable {index}</p>" });

    private async Task AssertSingleArticleAsync(string url)
    {
        var normalized = UrlNormalizer.Normalize(url);
        await using var db = fixture.CreateContext();
        (await db.Articles.IgnoreQueryFilters().CountAsync(a => a.NormalizedUrl == normalized)).Should().Be(1);
    }

    private async Task<Feed> SeedFeedAsync()
    {
        var feed = NewFeed(DateTimeOffset.UtcNow.AddDays(1));
        await using var db = fixture.CreateContext();
        db.Feeds.Add(feed);
        await db.SaveChangesAsync();
        return feed;
    }

    private static Feed NewFeed(DateTimeOffset nextFetchAt)
    {
        var feed = Feed.Create($"https://example.com/{Guid.NewGuid():N}", "Test feed", null, FeedFormat.Rss);
        feed.NextFetchAt = nextFetchAt;
        return feed;
    }

    private static Task<HttpResponseMessage> NotModified() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));

    private static FeedRefresher Refresher(MiniCcDbContext db, Func<Task<HttpResponseMessage>> respond)
    {
        var uow = new UnitOfWork(db);
        return new(db, uow, new FeedFetcher(new FeedClientFactory(respond)), new FeedParser(), new FeedImportService(db, uow));
    }

    private sealed class FeedClientFactory(Func<Task<HttpResponseMessage>> respond) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(respond));

        private sealed class Handler(Func<Task<HttpResponseMessage>> respond) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond();
        }
    }
}
