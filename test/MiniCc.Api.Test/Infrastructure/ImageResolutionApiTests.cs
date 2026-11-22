using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Worker;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Test.Images;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public sealed class ImageResolutionApiTests : IAsyncLifetime
{
    private const string Image = "https://images.example.test/photo.png";
    private const string Html = $"<p>Content</p><img src=\"{Image}\" width=\"320\">";
    private static readonly string Hash = new('a', 64);
    private static readonly string StoredUrl = $"/api/images/{Hash}.png";
    private readonly SqliteFixture fixture = new();

    public Task InitializeAsync() => fixture.InitializeAsync();
    public Task DisposeAsync() => fixture.DisposeAsync();

    [Fact]
    public async Task SaveAndDownload_DetailServesStoredBodyAndCover()
    {
        using var basis = SqliteApi.CreateFactory(fixture);
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10];
        using var factory = basis.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient("images").ConfigurePrimaryHttpMessageHandler(() => new ImageResponseHandler(png))));
        using var client = SqliteApi.AuthenticatedClient(factory);

        var saved = await Save(client, Guid.NewGuid().ToString("N"));
        var id = saved.GetProperty("id").GetGuid();
        saved.GetProperty("imageUrl").GetString().Should().Be(Image);

        using var scope = factory.Services.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<ImageWorkService>();
        var db = scope.ServiceProvider.GetRequiredService<MiniCcDbContext>();
        var now = DateTimeOffset.UtcNow;
        (await ImageLeases.ClaimDueAsync(db, now, default)).Should().Contain(id);
        (await work.ProcessAsync(id, now, default)).Should().Be(new ImageProcessResult(1, 0, 0));

        var storedUrl = $"/api/images/{Convert.ToHexStringLower(SHA256.HashData(png))}.png";
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/articles/{id}");
        detail.GetProperty("readableContent").GetString().Should().Contain($"src=\"{storedUrl}\"");
        detail.GetProperty("imageUrl").GetString().Should().Be(storedUrl);
        using var image = await client.GetAsync(storedUrl);
        image.StatusCode.Should().Be(HttpStatusCode.OK);
        image.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await image.Content.ReadAsByteArrayAsync()).Should().Equal(png);
    }

    [Fact]
    public async Task DetailAndMetadataUpdate_ReturnStoredBodyAndCover()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);

        var id = await SaveDoneArticle(client, Guid.NewGuid().ToString("N"));
        var path = $"/api/articles/{id}";
        var detail = await client.GetFromJsonAsync<JsonElement>(path);
        detail.GetProperty("readableContent").GetString().Should().Contain(StoredUrl);
        detail.GetProperty("imageUrl").GetString().Should().Be(StoredUrl);

        using var updated = await client.PutAsJsonAsync(path, new { title = "Updated title", summary = "" });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await updated.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("readableContent").GetString().Should().Contain(StoredUrl);
        dto.GetProperty("imageUrl").GetString().Should().Be(StoredUrl);
    }

    [Fact]
    public async Task ListStateRestoreAndTagResponses_ResolveCoverWithoutSerializingImageState()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var title = Guid.NewGuid().ToString("N");
        var id = await SaveDoneArticle(client, title);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=all&q={title}");
        AssertCover(list.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject);

        var path = $"/api/articles/{id}";
        using var state = await client.PatchAsJsonAsync(path + "/state", new { isFavorite = true, readingStatus = "read" });
        state.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCover(await state.Content.ReadFromJsonAsync<JsonElement>());

        using var removed = await client.DeleteAsync(path);
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var restored = await client.PostAsync(path + "/restore", null);
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertCover(await restored.Content.ReadFromJsonAsync<JsonElement>());

        using var tagged = await client.PostAsJsonAsync(path + "/tags", new { name = title });
        tagged.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await client.GetFromJsonAsync<JsonElement>($"/api/tags?search={title}");
        var tagId = tags.EnumerateArray().Single().GetProperty("id").GetGuid();
        var tag = await client.GetFromJsonAsync<JsonElement>($"/api/tags/{tagId}");
        var taggedArticle = tag.GetProperty("articles").EnumerateArray().Single();
        AssertCover(taggedArticle);
        taggedArticle.GetProperty("url").GetString().Should().BeEmpty();
        taggedArticle.GetProperty("readAt").ValueKind.Should().Be(JsonValueKind.Null);
        taggedArticle.GetProperty("tags").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ImageProxyMode_DetailAndListReturnSignedUrls()
    {
        using var basis = SqliteApi.CreateFactory(fixture);
        using var factory = basis.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Images:Mode"] = "ImageProxy" })));
        using var client = SqliteApi.AuthenticatedClient(factory);
        var title = Guid.NewGuid().ToString("N");
        var saved = await Save(client, title);
        var id = saved.GetProperty("id").GetGuid();
        var expectedImage = TestImages.Proxy().GetUrl(Image);

        saved.GetProperty("readableContent").GetString().Should().Be(TestImages.LegacyRewrite(Html));
        saved.GetProperty("imageUrl").GetString().Should().Be(expectedImage);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/articles/{id}");
        detail.GetProperty("readableContent").GetString().Should().Be(TestImages.LegacyRewrite(Html));
        detail.GetProperty("imageUrl").GetString().Should().Be(expectedImage);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=all&q={title}");
        list.GetProperty("items")[0].GetProperty("imageUrl").GetString().Should().Be(expectedImage);
    }

    [Theory]
    [InlineData(ImageMode.Store)]
    [InlineData(ImageMode.ImageProxy)]
    public async Task LegacyFeedCover_PreservesSourceBeforeAndAfterBackfill(ImageMode mode)
    {
        var cover = TestImages.Proxy(proxyUrl: "https://feed-images.test/cache").GetUrl(Image);
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.SetFeedContent(ContentKind.SummaryOnly, null);
            a.FillMissingMetadata("", "", cover);
        });
        await using (var db = fixture.CreateContext())
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Articles" SET "Images" = NULL WHERE "Id" = {article.Id}""");

        using var basis = SqliteApi.CreateFactory(fixture);
        using var factory = basis.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Images:Mode"] = mode.ToString() })));
        using var client = SqliteApi.AuthenticatedClient(factory);
        var expected = mode == ImageMode.Store ? cover : TestImages.Proxy().GetUrl(cover);

        await AssertCover();
        using var scope = factory.Services.CreateScope();
        var work = scope.ServiceProvider.GetRequiredService<ImageWorkService>();
        (await work.BackfillBatchAsync(DateTimeOffset.UtcNow, default)).Should().Be(1);
        await AssertCover();

        await using var read = fixture.CreateContext();
        var saved = await read.Articles.SingleAsync(a => a.Id == article.Id);
        saved.ImageUrl.Should().Be(cover);
        saved.Images!.Keys.Should().Equal(cover);

        async Task AssertCover()
        {
            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/articles/{article.Id}");
            detail.GetProperty("imageUrl").GetString().Should().Be(expected);
            var list = await client.GetFromJsonAsync<JsonElement>("/api/articles?view=all");
            list.GetProperty("items")[0].GetProperty("imageUrl").GetString().Should().Be(expected);
        }
    }

    private async Task<Guid> SaveDoneArticle(HttpClient client, string title)
    {
        var saved = await Save(client, title);
        saved.GetProperty("readableContent").GetString().Should().Contain("referrerpolicy=\"no-referrer\"");
        var id = saved.GetProperty("id").GetGuid();
        await using var db = fixture.CreateContext();
        var article = await db.Articles.SingleAsync(a => a.Id == id);
        article.ApplyImageResult(Image, new(ArticleImageState.Done, Hash: Hash, Ext: "png")).Should().BeTrue();
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<JsonElement> Save(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url = $"https://a.example.test/{title}", title, originalContent = "<p>Original</p>",
            readableContent = Html, imageUrl = Image
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertCover(JsonElement dto)
    {
        dto.GetProperty("imageUrl").GetString().Should().Be(StoredUrl);
        dto.TryGetProperty("images", out _).Should().BeFalse();
    }

    private sealed class ImageResponseHandler(byte[] png) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri.Should().Be(new Uri(Image));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png) });
        }
    }
}
