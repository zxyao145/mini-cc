using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

namespace MiniCc.Api.Test.Infrastructure;
public partial class LibraryApiTests
{
    private class FeedHttpHandler(string url, string title = "Test feed", string body = "short", string cover = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent($"<rss version='2.0' xmlns:media='http://search.yahoo.com/mrss/'><channel><title>{System.Security.SecurityElement.Escape(title)}</title><link>https://example.com</link><description>test</description><item><title>{System.Security.SecurityElement.Escape(title)}</title><guid isPermaLink='false'>id</guid><link>{System.Security.SecurityElement.Escape(url)}</link><description>{System.Security.SecurityElement.Escape(body)}</description><media:thumbnail url='{System.Security.SecurityElement.Escape(cover)}'/></item></channel></rss>") });
    }
    [Fact]
    public async Task FeedHttpLifecycle_ProjectsSourcesPreservesAnnotationsAndAcknowledgesAnchors()
    {
        var articleUrl = $"https://example.com/{Guid.NewGuid()}";
        using var factory = CreateFactory().WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddHttpClient("feeds").ConfigurePrimaryHttpMessageHandler(() => new FeedHttpHandler(articleUrl))));
        using var client = AuthenticatedClient(factory);
        var url = $"https://example.com/feed/{Guid.NewGuid()}";
        var preview = await client.PostAsJsonAsync("/api/feeds/preview", new { url }); preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var tag = Tag.Create("Feed " + Guid.NewGuid()); await using (var seed = fixture.CreateContext()) { seed.Tags.Add(tag); await seed.SaveChangesAsync(); }
        var created = await client.PostAsJsonAsync("/api/feeds", new { feedUrl = url, title = "Named feed", tagIds = new[] { tag.Id } }); created.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await created.Content.ReadFromJsonAsync<JsonElement>(); result.GetProperty("imported").GetInt32().Should().Be(1); var id = result.GetProperty("feed").GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync("/api/feeds", new { feedUrl = url, title = "Duplicate", tagIds = Array.Empty<Guid>() })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=feeds&feedId={id}&source=feed&sort=published_desc");
        list.GetProperty("total").GetInt32().Should().Be(1); var articleId = list.GetProperty("items")[0].GetProperty("id").GetGuid();
        list.GetProperty("items")[0].GetProperty("feeds")[0].GetProperty("title").GetString().Should().Be("Named feed");
        var detailBeforeUpgrade = await client.GetFromJsonAsync<JsonElement>($"/api/articles/{articleId}");
        detailBeforeUpgrade.GetProperty("feeds").GetArrayLength().Should().Be(1);
        detailBeforeUpgrade.GetProperty("feeds")[0].GetProperty("title").GetString().Should().Be("Named feed");
        var sharedBefore = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=feeds&source=extension&feedId={id}");
        sharedBefore.GetProperty("total").GetInt32().Should().Be(0);
        await using (var db = fixture.CreateContext())
        {
            var article = await db.Articles.SingleAsync(a => a.Id == articleId); db.Highlights.Add(Highlight.Create(articleId, "short", "keep note", 0, 5));
            article.MarkRead(DateTimeOffset.UtcNow); article.SetFavorite(true); article.SetArchived(true); article.ApplyProgress(new(3, 1, .5, 50), "old", DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        }
        var upgraded = await client.PostAsJsonAsync("/api/articles/content", new { url = articleUrl, title = "Full", originalContent = "full original", readableContent = "<p>new short body</p>" });
        upgraded.StatusCode.Should().Be(HttpStatusCode.Created); var full = await upgraded.Content.ReadFromJsonAsync<JsonElement>();
        full.GetProperty("contentKind").GetString().Should().Be("full"); full.GetProperty("progressTextOffset").ValueKind.Should().Be(JsonValueKind.Null);
        full.GetProperty("readingStatus").GetString().Should().Be("read"); full.GetProperty("tags").GetArrayLength().Should().Be(1);
        full.GetProperty("feeds")[0].GetProperty("id").GetGuid().Should().Be(id);
        var sharedAfter = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=feeds&source=extension&feedId={id}");
        sharedAfter.GetProperty("total").GetInt32().Should().Be(1);
        var metadata = await client.PutAsJsonAsync($"/api/articles/{articleId}", new { title = "Updated title", summary = "Updated summary" });
        metadata.StatusCode.Should().Be(HttpStatusCode.OK);
        (await metadata.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("feeds")[0].GetProperty("id").GetGuid().Should().Be(id);
        var highlight = full.GetProperty("highlights")[0]; highlight.GetProperty("anchorStatus").GetString().Should().Be("pending"); highlight.GetProperty("note").GetString().Should().Be("keep note");
        var hid = highlight.GetProperty("id").GetGuid();
        (await client.PutAsJsonAsync($"/api/highlights/{hid}/anchor", new { startOffset = 4, endOffset = 9 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/highlights/{hid}/anchor", new { startOffset = -1, endOffset = 9 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PatchAsJsonAsync($"/api/feeds/{id}", new { paused = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/feeds/{id}/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var trash = await client.PostAsync($"/api/feeds/{id}/trash-articles", null); (await trash.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("skipped").GetInt32().Should().Be(1);
        (await client.DeleteAsync($"/api/feeds/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/articles/{articleId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/feeds/preview", new { url = "file:///etc/passwd" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
    [Fact]
    public async Task FeedPreview_LongCanonicalUrl_CanBeCreatedAndImportsLongResolvedLink()
    {
        var url = "https://example.com/" + string.Concat(Enumerable.Range(0, 500).Select(i => (char)(0x4e00+i))) + Guid.NewGuid();
        using var factory = CreateFactory().WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddHttpClient("feeds").ConfigurePrimaryHttpMessageHandler(() => new FeedHttpHandler(url + "/article"))));
        using var client = AuthenticatedClient(factory);
        var preview = await client.PostAsJsonAsync("/api/feeds/preview", new { url }); preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var canonical = (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("feedUrl").GetString(); canonical!.Length.Should().BeGreaterThan(2000);
        var saved = await client.PostAsJsonAsync("/api/feeds", new { feedUrl = canonical, title = "Long", tagIds = Array.Empty<Guid>() }); saved.StatusCode.Should().Be(HttpStatusCode.Created);
        (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imported").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task FeedUpgrade_WaitsForProgressLockAndResetsWithNewerVersion()
    {
        var article = await fixture.SeedArticleAsync(a => a.SetFeedContent(ContentKind.SummaryOnly, null));
        using var factory = CreateFactory(); using var client = AuthenticatedClient(factory);
        await using var progress = fixture.CreateContext(); await using var tx = await progress.Database.BeginTransactionAsync();
        var newer = DateTimeOffset.UtcNow.AddMinutes(1); newer = new DateTimeOffset(newer.Ticks - newer.Ticks % 10, TimeSpan.Zero);
        await progress.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"ProgressUpdatedAt\" = {newer}, \"ProgressSessionId\" = 'other' WHERE \"Id\" = {article.Id}");
        var saving = client.PostAsJsonAsync("/api/articles/content", new { url = article.Url.Value, title = "Full", originalContent = "full", readableContent = "full" });
        await using var monitor = fixture.CreateContext(); var waiting = 0;
        try
        {
            for (var attempt = 0; attempt < 100 && waiting == 0; attempt++)
            {
                waiting = await monitor.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%Articles%'").SingleAsync();
                if (waiting == 0) await Task.Delay(20);
            }
            waiting.Should().BeGreaterThan(0);
        }
        finally { await tx.CommitAsync(); }
        var response = await saving; response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("progressUpdatedAt").GetDateTimeOffset().Should().BeAfter(newer);
    }

    [Fact]
    public async Task FeedEmojiMetadata_RoundTripsValidUnicodeThroughPostgres()
    {
        var url = $"https://example.com/{Guid.NewGuid()}";
        var emoji = "a" + string.Concat(Enumerable.Repeat("😀", 300));
        using var factory = CreateFactory().WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddHttpClient("feeds").ConfigurePrimaryHttpMessageHandler(() => new FeedHttpHandler(url, emoji, emoji))));
        using var client = AuthenticatedClient(factory);
        var response = await client.PostAsJsonAsync("/api/feeds", new { feedUrl = url + "/feed", title = "Emoji", tagIds = Array.Empty<Guid>() });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(); var id = json.GetProperty("feed").GetProperty("id").GetGuid();
        json.GetProperty("feed").GetProperty("originalTitle").GetString().Should().EndWith("😀");
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=feeds&feedId={id}");
        list.GetProperty("items")[0].GetProperty("title").GetString().Should().EndWith("😀");
        list.GetProperty("items")[0].GetProperty("summary").GetString().Should().EndWith("😀");
    }

    [Fact]
    public async Task ConcurrentFeedAndPlugin_KeepOneCanonicalArticle_AndDuplicateFeedReturns409()
    {
        var url = $"https://example.com/{Guid.NewGuid()}";
        using var factory = CreateFactory().WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddHttpClient("feeds").ConfigurePrimaryHttpMessageHandler(() => new FeedHttpHandler(url))));
        using var client = AuthenticatedClient(factory);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/feeds", new { feedUrl = url + "/feed", title = "One", tagIds = Array.Empty<Guid>() }),
            client.PostAsJsonAsync("/api/feeds", new { feedUrl = url + "/feed", title = "Two", tagIds = Array.Empty<Guid>() }),
            client.PostAsJsonAsync("/api/articles/content", new { url, title = "Plugin", originalContent = "original", readableContent = "full body" }));
        responses.Take(2).Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        responses[2].StatusCode.Should().Be(HttpStatusCode.Created);
        await using var verify = fixture.CreateContext();
        (await verify.Articles.CountAsync(a => a.NormalizedUrl == url)).Should().Be(1);
        var saved = await verify.Articles.SingleAsync(a => a.NormalizedUrl == url);
        saved.ContentKind.Should().Be(ContentKind.Full); saved.SavedFromExtensionAt.Should().NotBeNull();
        (await verify.FeedEntries.CountAsync(e => e.ArticleId == saved.Id)).Should().Be(1);
    }

    [Fact]
    public async Task FeedGeneratedLongCover_RoundTripsWhileExternalCoverLimitRemains()
    {
        var url = $"https://example.com/{Guid.NewGuid()}";
        var cover = "https://example.com/" + new string('界', 400) + ".png";
        using var factory = CreateFactory().WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddHttpClient("feeds").ConfigurePrimaryHttpMessageHandler(() => new FeedHttpHandler(url, cover: cover))));
        using var client = AuthenticatedClient(factory);
        var response = await client.PostAsJsonAsync("/api/feeds", new { feedUrl = url + "/feed", title = "Long cover", tagIds = Array.Empty<Guid>() });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(); result.GetProperty("imported").GetInt32().Should().Be(1);
        await using var db = fixture.CreateContext();
        var article = await db.Articles.SingleAsync(a => a.NormalizedUrl == url);
        article.ImageUrl.Should().Be(new Uri(cover).AbsoluteUri); article.ImageUrl.Length.Should().BeGreaterThan(2000);
        var external = await client.PostAsJsonAsync("/api/articles/content", new { url = url + "/external", title = "External", originalContent = "body", readableContent = "body", imageUrl = new Uri(cover).AbsoluteUri });
        external.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false, 64, HttpStatusCode.OK)]
    [InlineData(false, 65, HttpStatusCode.BadRequest)]
    [InlineData(true, 64, HttpStatusCode.OK)]
    [InlineData(true, 65, HttpStatusCode.BadRequest)]
    public async Task ProgressSessionId_Matches64CharacterStorageBoundary(bool reset, int length, HttpStatusCode expected)
    {
        var article = await fixture.SeedArticleAsync(); using var factory = CreateFactory(); using var client = AuthenticatedClient(factory);
        var sessionId = new string('s', length);
        var response = reset ? await client.PostAsJsonAsync($"/api/articles/{article.Id}/progress/reset", new { sessionId })
            : await client.PutAsJsonAsync($"/api/articles/{article.Id}/progress", new { sessionId, textOffset = 1, blockIndex = 0, blockRatio = .1, percent = 10 });
        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task ResetAcknowledgesBodyKindFromLockedRow_WhenUpgradeFollowsEarlierDetailRead()
    {
        var article = await fixture.SeedArticleAsync(a => a.SetFeedContent(ContentKind.SummaryOnly, null));
        using var factory = CreateFactory(); using var client = AuthenticatedClient(factory);
        (await client.GetFromJsonAsync<JsonElement>($"/api/articles/{article.Id}")).GetProperty("contentKind").GetString().Should().Be("summaryOnly");
        var summaryReset = await client.PostAsJsonAsync($"/api/articles/{article.Id}/progress/reset", new { sessionId = "summary-reader" });
        (await summaryReset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contentKind").GetString().Should().Be("summaryOnly");
        var upgraded = await client.PostAsJsonAsync("/api/articles/content", new { url = article.Url.Value, title = "Full", originalContent = "full", readableContent = "full" });
        upgraded.StatusCode.Should().Be(HttpStatusCode.Created);
        var response = await client.PostAsJsonAsync($"/api/articles/{article.Id}/progress/reset", new { sessionId = "old-summary-reader" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("contentKind", out var kind).Should().BeTrue(); kind.GetString().Should().Be("full");
    }
}
