using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.FeedNs.Application;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Test.Infrastructure;
[Collection("Postgres")]
public class FeedRefreshPostgresTests(PostgresFixture fixture)
{
    private class Factory(Func<Task<HttpResponseMessage>> respond) : IHttpClientFactory
    {
        private class Handler(Func<Task<HttpResponseMessage>> respond) : HttpMessageHandler
        { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => respond(); }
        public HttpClient CreateClient(string name) => new(new Handler(respond));
    }
    private FeedRefresher Refresher(MiniCcDbContext db, Func<Task<HttpResponseMessage>> respond)
    {
        var uow = new UnitOfWork(db);
        return new(db, uow, new FeedFetcher(new Factory(respond)), new FeedParser(new ImageProxyService(Options.Create(new ImageProxyOptions { SignatureKey = "test" }))), new FeedImportService(db, uow));
    }
    private async Task<Feed> Seed()
    { var feed = Feed.Create($"https://example.com/{Guid.NewGuid()}", "Test", null, FeedFormat.Rss); await using var db = fixture.CreateContext(); db.Feeds.Add(feed); await db.SaveChangesAsync(); return feed; }
    [Fact]
    public async Task Lease_IsAtomic_AndStaleOwnerCannotClearReclaimedLease()
    {
        var feed = await Seed();
        await using var one = fixture.CreateContext(); await using var two = fixture.CreateContext();
        var a = Refresher(one, () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)));
        var b = Refresher(two, () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)));
        var tokens = await Task.WhenAll(a.ClaimAsync(feed.Id, default), b.ClaimAsync(feed.Id, default));
        tokens.Count(t => t != null).Should().Be(1); var old = tokens.Single(t => t != null)!.Value;
        await one.Feeds.Where(f => f.Id == feed.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var current = (await b.ClaimAsync(feed.Id, default))!.Value;
        await a.RefreshAsync(feed.Id, old, default);
        (await one.Feeds.AsNoTracking().SingleAsync(f => f.Id == feed.Id)).LeaseToken.Should().Be(current);
    }
    [Theory]
    [InlineData(FeedStatus.Paused)][InlineData(FeedStatus.Unsubscribed)]
    public async Task StatusChangedDuringFetch_DiscardsResponse(FeedStatus status)
    {
        var feed = await Seed(); var started = new TaskCompletionSource(); var finish = new TaskCompletionSource<HttpResponseMessage>();
        await using var db = fixture.CreateContext(); var refresh = Refresher(db, () => { started.SetResult(); return finish.Task; });
        var token = (await refresh.ClaimAsync(feed.Id, default))!.Value;
        var running = refresh.RefreshAsync(feed.Id, token, default); await started.Task;
        await using var edit = fixture.CreateContext();
        await edit.Feeds.Where(f => f.Id == feed.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, status).SetProperty(f => f.LeaseToken, (Guid?)null).SetProperty(f => f.LeaseUntil, (DateTimeOffset?)null));
        finish.SetResult(new(HttpStatusCode.OK) { Content = new StringContent("<rss version='2.0'><channel><title>T</title><link>https://example.com</link><description>D</description><item><title>A</title><link>https://example.com/a</link><description>summary</description></item></channel></rss>") });
        await running; (await edit.FeedEntries.CountAsync(e => e.FeedId == feed.Id)).Should().Be(0);
        (await edit.Feeds.SingleAsync(f => f.Id == feed.Id)).Status.Should().Be(status);
    }
    [Fact]
    public async Task FailureBackoffRetryAfterAnd304_ArePersisted()
    {
        var feed = await Seed(); await using var db = fixture.CreateContext();
        var retryAt = DateTimeOffset.UtcNow.AddHours(3);
        var refresh = Refresher(db, () => { var r = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); r.Headers.RetryAfter = new(retryAt); return Task.FromResult(r); });
        await refresh.RefreshAsync(feed.Id, (await refresh.ClaimAsync(feed.Id, default))!.Value, default);
        db.ChangeTracker.Clear(); var failed = await db.Feeds.SingleAsync(f => f.Id == feed.Id);
        failed.ConsecutiveFailures.Should().Be(1); failed.NextFetchAt.Should().BeOnOrAfter(retryAt.AddSeconds(-1)); failed.LastError.Should().Contain("503"); failed.LeaseUntil.Should().BeNull();
        var success = Refresher(db, () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)));
        await success.RefreshAsync(feed.Id, (await success.ClaimAsync(feed.Id, default))!.Value, default);
        db.ChangeTracker.Clear(); var saved = await db.Feeds.SingleAsync(f => f.Id == feed.Id);
        saved.LastError.Should().BeNull(); saved.LastSuccessAt.Should().NotBeNull(); saved.ConsecutiveFailures.Should().Be(0);
        FeedRefresher.BackoffMinutes(15).Should().Be(1440);
    }

    [Fact]
    public async Task ClaimDue_IsAtomicAcrossWorkersAndLimitsEachBatchToFour()
    {
        var ids = new List<Guid>(); for (var i = 0; i < 6; i++) ids.Add((await Seed()).Id);
        await using var setup = fixture.CreateContext();
        await setup.Feeds.Where(f => ids.Contains(f.Id)).ExecuteUpdateAsync(s => s.SetProperty(f => f.NextFetchAt, DateTimeOffset.UtcNow.AddYears(-1)));
        await using var one = fixture.CreateContext(); await using var two = fixture.CreateContext();
        var a = Refresher(one, () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)));
        var b = Refresher(two, () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)));
        var results = await Task.WhenAll(a.ClaimDueAsync(Guid.NewGuid(), default), b.ClaimDueAsync(Guid.NewGuid(), default));
        results.Should().OnlyContain(batch => batch.Count <= 4);
        results[0].Intersect(results[1]).Should().BeEmpty();
        ids.Should().OnlyContain(id => results.SelectMany(batch => batch).Contains(id));
    }
    [Fact]
    public async Task SlowFailingFeed_DoesNotBlockAnotherScope()
    {
        var bad = await Seed(); var good = await Seed(); var finish = new TaskCompletionSource<HttpResponseMessage>();
        await using var one = fixture.CreateContext(); await using var two = fixture.CreateContext();
        var slow = Refresher(one, () => finish.Task);
        var healthy = Refresher(two, () => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified)));
        var running = slow.RefreshAsync(bad.Id, (await slow.ClaimAsync(bad.Id, default))!.Value, default);
        try
        {
            var fast = healthy.RefreshAsync(good.Id, (await healthy.ClaimAsync(good.Id, default))!.Value, default);
            (await fast.WaitAsync(TimeSpan.FromSeconds(3))).Status.Should().Be("ok");
            running.IsCompleted.Should().BeFalse();
        }
        finally { finish.SetResult(new(HttpStatusCode.InternalServerError)); }
        (await running).Status.Should().Be("failing");
    }

    [Fact]
    public async Task NotModified_AdoptsSuppliedValidatorsAndRetainsOmittedOnes()
    {
        var feed = await Seed(); await using var db = fixture.CreateContext();
        var initialDate = "Tue, 01 Jan 2030 00:00:00 GMT";
        await db.Feeds.Where(f => f.Id == feed.Id).ExecuteUpdateAsync(s => s.SetProperty(f => f.ETag, "\"v1\"").SetProperty(f => f.LastModified, initialDate));
        var refresh = Refresher(db, () => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified); response.Headers.ETag = new("\"v2\""); return Task.FromResult(response);
        });
        await refresh.RefreshAsync(feed.Id, (await refresh.ClaimAsync(feed.Id, default))!.Value, default);
        db.ChangeTracker.Clear(); var saved = await db.Feeds.AsNoTracking().SingleAsync(f => f.Id == feed.Id);
        saved.ETag.Should().Be("\"v2\""); saved.LastModified.Should().Be(initialDate);
        var modified = new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.Zero);
        refresh = Refresher(db, () => {
            var response = new HttpResponseMessage(HttpStatusCode.NotModified); response.Content.Headers.LastModified = modified; return Task.FromResult(response);
        });
        await refresh.RefreshAsync(feed.Id, (await refresh.ClaimAsync(feed.Id, default))!.Value, default);
        db.ChangeTracker.Clear(); saved = await db.Feeds.AsNoTracking().SingleAsync(f => f.Id == feed.Id);
        saved.ETag.Should().Be("\"v2\""); saved.LastModified.Should().Be(modified.ToString("R"));
    }
}
