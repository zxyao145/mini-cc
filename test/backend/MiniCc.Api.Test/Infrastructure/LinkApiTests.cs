using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Test.Infrastructure;

public partial class LibraryApiTests
{
    [Fact]
    public async Task SaveLink_CreatesSearchableLink_AndDeduplicatesByNormalizedUrl()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var url = $"https://www.youtube.com/watch?v={Guid.NewGuid():N}";
        var saved = await client.PostAsJsonAsync("/api/articles/link", new { url, title = "Video", summary = "zqxlinktoken" });
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        var link = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var id = link.GetProperty("id").GetGuid();
        saved.Headers.Location!.AbsolutePath.Should().BeEquivalentTo($"/api/articles/{id}");
        link.GetProperty("contentKind").GetString().Should().Be("link");
        link.GetProperty("readableContent").GetString().Should().BeEmpty();
        link.GetProperty("savedFromExtensionAt").ValueKind.Should().Be(JsonValueKind.String);

        var duplicate = await client.PostAsJsonAsync("/api/articles/link", new { url = url + "&utm_source=x#frag", title = "Duplicate" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Created);
        var duplicateLink = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        duplicateLink.GetProperty("id").GetGuid().Should().Be(id);
        duplicateLink.GetProperty("url").GetString().Should().Be(url);

        var playlistUrl = url + "&list=PL1#t=1";
        var playlist = await client.PostAsJsonAsync("/api/articles/link", new { url = playlistUrl, title = "Playlist" });
        playlist.StatusCode.Should().Be(HttpStatusCode.Created);
        var playlistLink = await playlist.Content.ReadFromJsonAsync<JsonElement>();
        playlistLink.GetProperty("id").GetGuid().Should().NotBe(id);
        playlistLink.GetProperty("url").GetString().Should().Be(playlistUrl);

        var page = await client.GetFromJsonAsync<JsonElement>("/api/articles?view=all&q=zqxlinktoken");
        page.GetProperty("items").EnumerateArray().Should().Contain(item => item.GetProperty("id").GetGuid() == id);
        var batch = await client.PostAsJsonAsync("/api/articles/batch", new { ids = new[] { id }, action = "markRead" });
        batch.StatusCode.Should().Be(HttpStatusCode.OK);
        var batchResult = await batch.Content.ReadFromJsonAsync<JsonElement>();
        batchResult.GetProperty("succeeded").EnumerateArray().Should().Contain(item => item.GetGuid() == id);
    }

    [Fact]
    public async Task SaveLink_RestoresTrashedItemAndKeepsState()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var input = new { url = $"https://example.com/{Guid.NewGuid()}", title = "Video" };
        var saved = await client.PostAsJsonAsync("/api/articles/link", input);
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var path = $"/api/articles/{id}";
        (await client.PatchAsJsonAsync(path + "/state", new { isFavorite = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        var tagName = $"Link {Guid.NewGuid():N}";
        (await client.PostAsJsonAsync(path + "/tags", new { name = tagName })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var restored = await client.PostAsJsonAsync("/api/articles/link", input);

        restored.StatusCode.Should().Be(HttpStatusCode.Created);
        var link = await restored.Content.ReadFromJsonAsync<JsonElement>();
        link.GetProperty("id").GetGuid().Should().Be(id);
        link.GetProperty("deletedAt").ValueKind.Should().Be(JsonValueKind.Null);
        link.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        link.GetProperty("tags").EnumerateArray().Should().Contain(tag => tag.GetProperty("name").GetString() == tagName);
    }

    [Theory]
    [InlineData(ContentKind.Full, "full")]
    [InlineData(ContentKind.SummaryOnly, "summaryOnly")]
    public async Task SaveLink_PreservesExistingArticleContent(ContentKind kind, string jsonKind)
    {
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.SetFeedContent(kind, null);
            a.UpdateMetadata("Original title", "Original summary");
        });
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/articles/link", new { url = article.Url.Value, title = "New title", summary = "New summary" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("id").GetGuid().Should().Be(article.Id);
        result.GetProperty("contentKind").GetString().Should().Be(jsonKind);
        result.GetProperty("readableContent").GetString().Should().Be("readable");
        result.GetProperty("summary").GetString().Should().Be("Original summary");
        result.GetProperty("title").GetString().Should().Be("Original title");
    }

    [Theory]
    [InlineData("ftp://example.com/video", "Video")]
    [InlineData("https://example.com/video", "   ")]
    [InlineData("https://example.com/video", null)]
    public async Task SaveLink_RejectsInvalidInput(string url, string? title)
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var body = new Dictionary<string, string> { ["url"] = url };
        if (title != null) body["title"] = title;

        var response = await client.PostAsJsonAsync("/api/articles/link", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ContentSave_UpgradesLinkToFull_KeepingStateAndTags()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var url = $"https://example.com/{Guid.NewGuid()}";
        var saved = await client.PostAsJsonAsync("/api/articles/link", new { url, title = "Saved as link", summary = "Kept summary" });
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var path = $"/api/articles/{id}";
        var tagName = $"Link {Guid.NewGuid():N}";
        (await client.PatchAsJsonAsync(path + "/state", new { isFavorite = true, isArchived = true, readingStatus = "read" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(path + "/tags", new { name = tagName })).StatusCode.Should().Be(HttpStatusCode.OK);

        var upgraded = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url,
            title = "Article title",
            author = "Jane",
            summary = "New summary",
            imageUrl = "https://example.com/cover.png",
            originalContent = "<html>Original</html>",
            readableContent = "<p>Article body</p>"
        });

        upgraded.StatusCode.Should().Be(HttpStatusCode.Created);
        var article = await upgraded.Content.ReadFromJsonAsync<JsonElement>();
        article.GetProperty("id").GetGuid().Should().Be(id);
        article.GetProperty("contentKind").GetString().Should().Be("full");
        article.GetProperty("readableContent").GetString().Should().Be("<p>Article body</p>");
        article.GetProperty("title").GetString().Should().Be("Saved as link");
        article.GetProperty("author").GetString().Should().Be("Jane");
        article.GetProperty("summary").GetString().Should().Be("Kept summary");
        article.GetProperty("imageUrl").GetString().Should().Contain("/imageProxy/").And.EndWith("https://example.com/cover.png");
        article.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        article.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        article.GetProperty("readingStatus").GetString().Should().Be("read");
        article.GetProperty("tags").EnumerateArray().Should().Contain(tag => tag.GetProperty("name").GetString() == tagName);
    }

    [Fact]
    public async Task ProgressAndHighlights_AreRejectedForLinks()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var saved = await client.PostAsJsonAsync("/api/articles/link", new { url = $"https://example.com/{Guid.NewGuid()}", title = "Link" });
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var path = $"/api/articles/{id}";
        var progress = new { sessionId = "s", textOffset = 2, blockIndex = 0, blockRatio = .2, percent = 15 };

        (await client.PutAsJsonAsync(path + "/progress", progress)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(path + "/progress/reset", new { sessionId = "s" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(path + "/highlights", new { text = "Example", note = "Note", startOffset = 0, endOffset = 7 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/articles/{Guid.NewGuid()}/progress", progress)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var article = await client.GetFromJsonAsync<JsonElement>(path);
        article.GetProperty("progressUpdatedAt").ValueKind.Should().Be(JsonValueKind.Null);
        article.GetProperty("highlights").GetArrayLength().Should().Be(0);
        article.GetProperty("readingStatus").GetString().Should().Be("unread");
    }
}
