using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.FeedNs.Application;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Shared.Data.Repositories;
using MiniCc.Api.Test.Infrastructure;

namespace MiniCc.Api.Test.Images;

[Collection("Sqlite")]
public class ImageWritePathTests(SqliteFixture fixture)
{
    private const string BodyUrl = "https://img.test/a.png";
    private const string CoverUrl = "https://img.test/c.jpg";
    private const string Body = $"<p>Saved body</p><img src=\"{BodyUrl}\">";

    [Fact]
    public async Task SaveContent_StoresNormalizedBodyAndCoverWithPendingImages()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var id = await Save(client, $"https://example.test/{Guid.NewGuid():N}", CoverUrl);

        await using var db = fixture.CreateContext();
        var article = await db.Articles.SingleAsync(a => a.Id == id);
        article.ReadableContent.Should().Be(Body);
        article.ImageUrl.Should().Be(CoverUrl);
        article.OriginalContent.Should().Be("<p>Original</p>");
        article.Images!.Keys.Should().BeEquivalentTo([BodyUrl, CoverUrl]);
        article.Images.Values.Should().OnlyContain(image => image.State == ArticleImageState.Pending);
        article.ImagesDueAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Resave_ResetsFailedToPending()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var url = $"https://example.test/{Guid.NewGuid():N}";
        var id = await Save(client, url);
        await using (var db = fixture.CreateContext())
        {
            var article = await db.Articles.SingleAsync(a => a.Id == id);
            article.ApplyImageResult(BodyUrl, new(ArticleImageState.Failed, Reason: "notFound")).Should().BeTrue();
            await db.SaveChangesAsync();
        }

        (await Save(client, url)).Should().Be(id);

        await using var read = fixture.CreateContext();
        var saved = await read.Articles.SingleAsync(a => a.Id == id);
        saved.Images![BodyUrl].State.Should().Be(ArticleImageState.Pending);
        saved.Images[BodyUrl].Attempts.Should().Be(0);
        saved.ImagesDueAt.Should().Be(saved.Images[BodyUrl].NextAttemptAt);
    }

    [Fact]
    public async Task FeedImport_DoesNotResetFailed()
    {
        var url = $"https://example.test/{Guid.NewGuid():N}";
        var feed = Feed.Create(url + "/feed", "Source", null, FeedFormat.Rss);
        feed.NextFetchAt = DateTimeOffset.UtcNow.AddHours(1);
        await using var db = fixture.CreateContext();
        db.Feeds.Add(feed);
        await db.SaveChangesAsync();
        var unit = new UnitOfWork(db);
        var importer = new FeedImportService(db, unit);
        var item = new ParsedEntry("post", url, "Title", "", "<p>Original</p>", Body, "", CoverUrl, 10, ContentKind.Full, null);
        (await importer.ImportAsync(feed.Id, [item], true, default)).Imported.Should().Be(1);

        var article = await db.Articles.SingleAsync(a => a.NormalizedUrl == url);
        article.Images!.Keys.Should().BeEquivalentTo([BodyUrl, CoverUrl]);
        var failed = new ArticleImage(ArticleImageState.Failed, Reason: "notFound");
        article.ApplyImageResult(CoverUrl, failed).Should().BeTrue();
        await db.SaveChangesAsync();

        await importer.ImportAsync(feed.Id, [item], false, default);

        db.ChangeTracker.Clear();
        (await db.Articles.SingleAsync(a => a.Id == article.Id)).Images![CoverUrl].Should().Be(failed);
    }

    [Fact]
    public async Task SaveLink_ExistingLegacyRow_BackfillsBeforeSync()
    {
        var article = await LegacyArticle(TestImages.Proxy().GetUrl(CoverUrl), savedFromExtension: true);
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        using var result = await client.PostAsJsonAsync("/api/articles/link", new { url = article.Url.Value, title = "Bookmark" });
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var read = fixture.CreateContext();
        var saved = await read.Articles.SingleAsync(a => a.Id == article.Id);
        saved.ReadableContent.Should().Be(Body);
        saved.ImageUrl.Should().Be(CoverUrl);
        saved.Images!.Keys.Should().BeEquivalentTo([BodyUrl, CoverUrl]);
    }

    [Fact]
    public async Task Resave_LegacyProxiedArticleWithSameContent_KeepsRevisionAndProgress()
    {
        var article = await LegacyArticle(TestImages.Proxy().GetUrl(CoverUrl), savedFromExtension: true);
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);

        (await Save(client, article.Url.Value)).Should().Be(article.Id);

        await using var read = fixture.CreateContext();
        var saved = await read.Articles.SingleAsync(a => a.Id == article.Id);
        saved.ContentRevision.Should().Be(article.ContentRevision);
        saved.ProgressTextOffset.Should().Be(article.ProgressTextOffset);
        saved.ProgressBlockIndex.Should().Be(article.ProgressBlockIndex);
        saved.ProgressBlockRatio.Should().Be(article.ProgressBlockRatio);
        saved.ProgressPercent.Should().Be(article.ProgressPercent);
        saved.ProgressUpdatedAt.Should().Be(article.ProgressUpdatedAt);
        saved.ReadableContent.Should().Be(ImageHtml.Normalize(Body));
        saved.Images!.Keys.Should().BeEquivalentTo([BodyUrl, CoverUrl]);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("link")]
    public async Task Save_LegacyFeedCoverWithoutReplacement_KeepsSource(string route)
    {
        var cover = TestImages.Proxy(proxyUrl: "https://feed-images.test/cache").GetUrl(CoverUrl);
        var article = await LegacyArticle(cover, savedFromExtension: false);
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        using var result = await client.PostAsJsonAsync($"/api/articles/{route}", new
        {
            url = article.Url.Value, title = "Saved image", originalContent = "<p>Original</p>", readableContent = Body
        });
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var db = fixture.CreateContext();
        var saved = await db.Articles.SingleAsync(a => a.Id == article.Id);
        saved.SavedFromExtensionAt.Should().NotBeNull();
        saved.ImageUrl.Should().Be(cover);
        saved.Images!.Keys.Should().BeEquivalentTo([BodyUrl, cover]);
    }

    private async Task<Article> LegacyArticle(string cover, bool savedFromExtension)
    {
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.ReplaceContent(Content.Create("<p>Original</p>", TestImages.LegacyRewrite(Body)), ContentKind.Full);
            a.FillMissingMetadata("", "", cover);
            if (savedFromExtension) a.MarkSavedFromExtension(DateTimeOffset.UtcNow);
            a.ApplyProgress(new(3, 1, 0.3, 0.5), "reader", DateTimeOffset.UtcNow);
        });
        await using var db = fixture.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Articles" SET "Images" = NULL WHERE "Id" = {article.Id}""");
        return article;
    }

    private static async Task<Guid> Save(HttpClient client, string url, string imageUrl = "")
    {
        using var response = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url, title = "Saved image", originalContent = "<p>Original</p>", readableContent = Body, imageUrl,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
