using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniCc.Api.Controllers.ArticleNs;
using MiniCc.Api.Core.ApiKeys.Application.Services;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public partial class LibraryApiTests(PostgresFixture fixture)
{
    [Fact]
    public async Task StateTrashRestore_UsesRealHttpValidationAndPreservesIndependentState()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync();
        var path = $"/api/articles/{article.Id}";

        var first = await client.PatchAsJsonAsync(path + "/state", new { isFavorite = true, isArchived = true, readingStatus = "read" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await first.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("readingStatus").GetString().Should().Be("read");
        json.GetProperty("contentKind").GetString().Should().Be("full");
        var readAt = json.GetProperty("readAt").GetString();
        var again = await client.PatchAsJsonAsync(path + "/state", new { isFavorite = true, isArchived = true, readingStatus = "read" });
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("readAt").GetString().Should().Be(readAt);
        (await client.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PatchAsJsonAsync(path + "/state", new { isFavorite = false })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var restored = await client.PostAsync(path + "/restore", null);
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        json = await restored.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        json.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        json.GetProperty("readingStatus").GetString().Should().Be("read");
        (await client.PostAsync(path + "/favorite", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync(path + "/archive", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync(path + "/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_WhenConcurrentPurgeCommitsFirst_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        await using var blocker = fixture.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        (await blocker.Articles.IgnoreQueryFilters().Where(a => a.Id == article.Id).ExecuteDeleteAsync())
            .Should().Be(1);

        var restore = client.PostAsync($"/api/articles/{article.Id}/restore", null);
        try { await WaitForArticleLockAsync(); }
        finally { await transaction.CommitAsync(); }

        using var response = await restore;
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExtensionResave_TrashedFull_RestoresAndReplaces()
    {
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.AddTag(Tag.Create(Guid.NewGuid().ToString()));
            a.AddHighlight(Highlight.Create(a.Id, "text", "retained note", 0, 4));
            a.SetFavorite(true);
            a.SetArchived(true);
            a.MarkRead(DateTimeOffset.UtcNow);
            a.ApplyProgress(new ReadingPosition(17, 1, .4, 80), "reading-session", DateTimeOffset.UtcNow);
            a.MoveToTrash(DateTimeOffset.UtcNow);
        });
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url = article.Url.Value + "/?utm_source=extension#fragment",
            title = "Replacement title",
            originalContent = "replacement original",
            readableContent = "replacement readable"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().Be(article.Id);
        json.GetProperty("deletedAt").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("savedFromExtensionAt").ValueKind.Should().Be(JsonValueKind.String);
        json.GetProperty("readingStatus").GetString().Should().Be("read");
        json.GetProperty("progress").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("progressUpdatedAt").GetDateTimeOffset().Should().BeAfter(article.ProgressUpdatedAt!.Value);
        json.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        json.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        json.GetProperty("title").GetString().Should().Be("Replacement title");
        json.GetProperty("readableContent").GetString().Should().Be("replacement readable");
        json.GetProperty("contentRevision").GetInt32().Should().Be(article.ContentRevision + 1);
        json.GetProperty("tags").GetArrayLength().Should().Be(1);
        json.GetProperty("highlights")[0].GetProperty("note").GetString().Should().Be("retained note");
        json.GetProperty("highlights")[0].GetProperty("anchorStatus").GetString().Should().Be("pending");
        await using var db = fixture.CreateContext();
        (await db.Articles.CountAsync(a => a.NormalizedUrl == article.NormalizedUrl)).Should().Be(1);
    }

    [Fact]
    public async Task ExtensionResave_FullWithNewBody_ReplacesContentAndPreservesState()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var url = $"https://example.com/{Guid.NewGuid()}";
        const string savePath = "/api/articles/content";
        var saved = await client.PostAsJsonAsync(savePath, new { url, title = "First", originalContent = "first original", readableContent = "first body" });
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        var original = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        var detailPath = $"/api/articles/{id}";
        var tag = Tag.Create(Guid.NewGuid().ToString());
        await using (var db = fixture.CreateContext())
        {
            var article = await db.Articles.SingleAsync(a => a.Id == id);
            db.Tags.Add(tag);
            article.AddTag(tag);
            db.Highlights.Add(Highlight.Create(id, "first", "keep note", 0, 5));
            article.SetFavorite(true);
            await db.SaveChangesAsync();
        }
        var progress = await client.PutAsJsonAsync(detailPath + "/progress", new { sessionId = "old", textOffset = 5 });
        progress.StatusCode.Should().Be(HttpStatusCode.OK);
        var version = (await progress.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("progressUpdatedAt").GetDateTimeOffset();

        var replaced = await client.PostAsJsonAsync(savePath, new { url, title = "Second", originalContent = "second original", readableContent = "second body" });
        replaced.StatusCode.Should().Be(HttpStatusCode.Created);
        (await replaced.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid().Should().Be(id);
        var detail = await client.GetFromJsonAsync<JsonElement>(detailPath);
        detail.GetProperty("title").GetString().Should().Be("Second");
        detail.GetProperty("readableContent").GetString().Should().Be("second body");
        detail.GetProperty("contentRevision").GetInt32().Should().Be(original.GetProperty("contentRevision").GetInt32() + 1);
        detail.GetProperty("savedFromExtensionAt").GetDateTimeOffset().Should().Be(original.GetProperty("savedFromExtensionAt").GetDateTimeOffset());
        detail.GetProperty("readingStatus").GetString().Should().Be("reading");
        detail.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        detail.GetProperty("tags")[0].GetProperty("id").GetGuid().Should().Be(tag.Id);
        detail.GetProperty("highlights")[0].GetProperty("anchorStatus").GetString().Should().Be("pending");
        detail.GetProperty("highlights")[0].GetProperty("note").GetString().Should().Be("keep note");
        detail.GetProperty("progress").ValueKind.Should().Be(JsonValueKind.Null);
        detail.GetProperty("progressUpdatedAt").GetDateTimeOffset().Should().BeAfter(version);
    }

    [Fact]
    public async Task LinkSave_OnExistingFull_LeavesBodyAndRevisionUnchanged()
    {
        var article = await fixture.SeedArticleAsync();
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/articles/link", new { url = article.Url.Value, title = "Link title" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/articles/{article.Id}");
        detail.GetProperty("contentKind").GetString().Should().Be("full");
        detail.GetProperty("contentRevision").GetInt32().Should().Be(article.ContentRevision);
        detail.GetProperty("readableContent").GetString().Should().Be(article.ReadableContent);
        detail.GetProperty("title").GetString().Should().Be(article.Title);
    }

    [Fact]
    public async Task FullResave_WaitsForProgressLockAndResetsWithNewerVersion()
    {
        var article = await fixture.SeedArticleAsync();
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        await using var progress = fixture.CreateContext();
        await using var tx = await progress.Database.BeginTransactionAsync();
        var future = DateTimeOffset.UtcNow.AddMinutes(1);
        var newer = future.AddTicks(-future.Ticks % 10);
        await progress.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"ProgressUpdatedAt\" = {newer}, \"ProgressSessionId\" = 'reader', \"ProgressTextOffset\" = 8 WHERE \"Id\" = {article.Id}");

        var saving = client.PostAsJsonAsync("/api/articles/content", new
        {
            url = article.Url.Value, title = "Replacement", originalContent = "full", readableContent = "full"
        });
        try { await WaitForArticleLockAsync(); }
        finally { await tx.CommitAsync(); }

        var response = await saving;
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("progressUpdatedAt").GetDateTimeOffset().Should().Be(newer.AddTicks(10));
        detail.GetProperty("progressTextOffset").ValueKind.Should().Be(JsonValueKind.Null);
        detail.GetProperty("contentRevision").GetInt32().Should().Be(article.ContentRevision + 1);
    }

    [Fact]
    public async Task FullResave_InvalidatesAnchorCommittedWhileWaitingForArticleLock()
    {
        var article = await fixture.SeedArticleAsync(a =>
        {
            var highlight = Highlight.Create(a.Id, "readable", "keep note", 0, 8);
            highlight.AnchorStatus = AnchorStatus.Pending;
            a.AddHighlight(highlight);
        });
        var highlightId = article.Highlights.Single().Id;
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        await using var anchoring = fixture.CreateContext();
        await using var tx = await anchoring.Database.BeginTransactionAsync();
        await anchoring.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Articles\" WHERE \"Id\" = {article.Id} FOR UPDATE");
        await anchoring.Highlights.Where(h => h.Id == highlightId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.AnchorStatus, AnchorStatus.Ok));

        var saving = client.PostAsJsonAsync("/api/articles/content", new
        {
            url = article.Url.Value, title = "Replacement", originalContent = "full", readableContent = "full"
        });
        try { await WaitForArticleLockAsync(); }
        finally { await tx.CommitAsync(); }

        (await saving).StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/articles/{article.Id}");
        detail.GetProperty("highlights")[0].GetProperty("anchorStatus").GetString().Should().Be("pending");
    }

    [Theory]
    [InlineData("path")]
    [InlineData("query")]
    [InlineData("authority")]
    public async Task ExtensionSave_MaximumLengthUrl_PersistsAndMatchesCanonicalVariant(string kind)
    {
        var prefix = kind == "query" ? "https://example.com?x=" : "https://example.com/";
        var url = kind == "authority" ? "https://" + new string('a', 1980) + "@example.com"
            : prefix + string.Concat(Enumerable.Range(0, 2000 - prefix.Length)
                .Select(i => (char)(0x4e00 + i * 7919 % 20992)));
        var canonical = new Uri(url).AbsoluteUri;
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var saved = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url, title = "Unicode URL", originalContent = "original", readableContent = "readable"
        });
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        var original = await saved.Content.ReadFromJsonAsync<JsonElement>();
        original.GetProperty("normalizedUrl").GetString().Should().Be(canonical);

        var duplicate = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url = url.Replace("example.com", "EXAMPLE.com"),
            title = "Duplicate", originalContent = "new original", readableContent = "new readable"
        });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Created);
        (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid()
            .Should().Be(original.GetProperty("id").GetGuid());
        await using var db = fixture.CreateContext();
        (await db.Articles.CountAsync(a => a.NormalizedUrl == canonical)).Should().Be(1);
        var oversized = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url = url + "x", title = "Too long", originalContent = "original", readableContent = "readable"
        });
        oversized.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"readingStatus\":\"reading\"}")]
    [InlineData("{\"readingStatus\":2}")]
    [InlineData("{\"isFavorite\":\"true\"}")]
    [InlineData("null")]
    public async Task State_RejectsEmptyOrInvalidBody(string body)
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var response = await client.PatchAsync($"/api/articles/{Guid.NewGuid()}/state", new StringContent(body, Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LibrarySettings_RequiresAuthenticationAndReturnsConfiguredValues()
    {
        using var factory = CreateFactory();
        using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await anonymous.GetAsync("/api/settings/library")).IsSuccessStatusCode.Should().BeFalse();
        using var client = AuthenticatedClient(factory);
        var response = await client.GetAsync("/api/settings/library");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("trashRetentionDays").GetInt32().Should().Be(45);
        json.GetProperty("undoWindowSeconds").GetInt32().Should().Be(12);
    }

    [Theory]
    [InlineData("view=unsupported")]
    [InlineData("view=invalid")]
    [InlineData("status=reading,unknown")]
    [InlineData("tags=invalid")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    [InlineData("sort=invalid")]
    [InlineData("cursor=invalid")]
    [InlineData("savedFrom=invalid")]
    [InlineData("savedFrom=2026-11-08T00:00:00Z&savedTo=2026-11-07T00:00:00Z")]
    public async Task ArticleList_RejectsInvalidQueries(string query)
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        (await client.GetAsync("/api/articles?" + query)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ArticleList_ProjectsPageAndTagEndpointIncludesArchivedWithoutArticleBodies()
    {
        var tag = Tag.Create(Guid.NewGuid().ToString());
        var article = await fixture.SeedArticleAsync(a => { a.AddTag(tag); a.SetArchived(true); a.AddHighlight(Highlight.Create(a.Id, "text", "secret note", 0, 4)); });
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var response = await client.GetAsync($"/api/tags/{tag.Id}/articles?pageSize=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("total").GetInt32().Should().Be(1);
        json.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        var row = json.GetProperty("items")[0];
        row.GetProperty("id").GetGuid().Should().Be(article.Id);
        row.GetProperty("highlightCount").GetInt32().Should().Be(1);
        row.GetProperty("feeds").GetArrayLength().Should().Be(0);
        row.TryGetProperty("readableContent", out _).Should().BeFalse();
        row.TryGetProperty("originalContent", out _).Should().BeFalse();
        row.TryGetProperty("highlights", out _).Should().BeFalse();
        row.GetProperty("tags").GetArrayLength().Should().Be(1);
        (await client.GetAsync("/api/articles?q=" + new string('a', 201))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemovedLightRoute_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        (await client.GetAsync("/api/articles/light")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Progress_HttpConflictValidationAndResetDetailVersion()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync();
        var path = $"/api/articles/{article.Id}";
        var progress = new { textOffset = 10, blockIndex = 2, blockRatio = .5, percent = 42, sessionId = "one", baseUpdatedAt = (string?)null };
        var first = await client.PutAsJsonAsync(path + "/progress", progress);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = await first.Content.ReadFromJsonAsync<JsonElement>();
        saved.GetProperty("readingStatus").GetString().Should().Be("reading");
        var conflict = await client.PutAsJsonAsync(path + "/progress", new { textOffset = 20, blockIndex = 3, blockRatio = .5, percent = 50, sessionId = "two" });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current").GetProperty("percent").GetDouble().Should().Be(42);
        var detail = await client.GetFromJsonAsync<JsonElement>(path);
        detail.GetProperty("progress").GetProperty("textOffset").GetInt32().Should().Be(10);
        detail.GetProperty("feeds").GetArrayLength().Should().Be(0);
        detail.GetProperty("savedFromExtension").GetBoolean().Should().BeFalse();
        var reset = await client.PostAsJsonAsync(path + "/progress/reset", new { sessionId = "two" });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        var resetJson = await reset.Content.ReadFromJsonAsync<JsonElement>();
        detail = await client.GetFromJsonAsync<JsonElement>(path);
        detail.GetProperty("progress").ValueKind.Should().Be(JsonValueKind.Null);
        detail.GetProperty("progressUpdatedAt").GetString().Should().Be(resetJson.GetProperty("progressUpdatedAt").GetString());
        detail.GetProperty("progressSessionId").GetString().Should().Be("two");
        foreach (var body in new[] { "{}", "{\"sessionId\":\"one\",\"percent\":101}", "{\"sessionId\":\"one\",\"blockRatio\":-1}", "{\"sessionId\":\"one\",\"blockIndex\":-1}", "{\"sessionId\":\"one\",\"textOffset\":-1}" })
            (await client.PutAsync(path + "/progress", new StringContent(body, Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(path + "/progress/reset", new { sessionId = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/api/articles/{Guid.NewGuid()}/progress", progress)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await client.DeleteAsync(path);
        (await client.PutAsJsonAsync(path + "/progress", progress)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync(path + "/progress/reset", new { sessionId = "one" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Progress_ResponsesCarryContentRevisionFromLockedRow()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync();
        var path = $"/api/articles/{article.Id}";

        var detail = await client.GetFromJsonAsync<JsonElement>(path);
        detail.GetProperty("contentRevision").GetInt32().Should().Be(article.ContentRevision);

        var save = await client.PutAsJsonAsync(path + "/progress", new { sessionId = "one", textOffset = 10, article.ContentRevision });
        save.StatusCode.Should().Be(HttpStatusCode.OK);
        (await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contentRevision").GetInt32().Should().Be(article.ContentRevision);

        var reset = await client.PostAsJsonAsync(path + "/progress/reset", new { sessionId = "one", article.ContentRevision });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("contentRevision").GetInt32().Should().Be(article.ContentRevision);

        var conflict = await client.PutAsJsonAsync(path + "/progress", new { sessionId = "two", textOffset = 15 });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current").GetProperty("contentRevision")
            .GetInt32().Should().Be(article.ContentRevision);
    }

    [Fact]
    public async Task Progress_StaleRevisionCannotWriteOrResetEvenInTheSameSession()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync(a => a.ApplyProgress(new ReadingPosition(10, 2, .5, 42), "one", DateTimeOffset.UtcNow));
        var path = $"/api/articles/{article.Id}";
        var contentRevision = article.ContentRevision - 1;

        var save = await client.PutAsJsonAsync(path + "/progress", new { sessionId = "one", textOffset = 90, contentRevision });
        var reset = await client.PostAsJsonAsync(path + "/progress/reset", new { sessionId = "one", contentRevision });
        foreach (var response in new[] { save, reset })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var current = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current");
            current.GetProperty("contentRevision").GetInt32().Should().Be(article.ContentRevision);
            current.GetProperty("textOffset").GetInt32().Should().Be(10);
        }
        var detail = await client.GetFromJsonAsync<JsonElement>(path);
        detail.GetProperty("progress").GetProperty("textOffset").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task HighlightAnchor_RejectsStaleRevisionAndKeepsHighlightPending()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync(a => a.AddHighlight(Highlight.Create(a.Id, "text", "keep note", 0, 4)));
        var highlight = article.Highlights.Single();
        await using (var db = fixture.CreateContext())
        {
            await db.Highlights.Where(h => h.Id == highlight.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.AnchorStatus, AnchorStatus.Pending));
        }
        var path = $"/api/highlights/{highlight.Id}/anchor";

        var stale = await client.PutAsJsonAsync(path, new { startOffset = 8, endOffset = 12, contentRevision = article.ContentRevision - 1 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current").GetProperty("contentRevision")
            .GetInt32().Should().Be(article.ContentRevision);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/articles/{article.Id}");
        detail.GetProperty("highlights")[0].GetProperty("anchorStatus").GetString().Should().Be("pending");

        var fresh = await client.PutAsJsonAsync(path, new { startOffset = 2, endOffset = 6, contentRevision = article.ContentRevision });
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var anchored = await fresh.Content.ReadFromJsonAsync<JsonElement>();
        anchored.GetProperty("anchorStatus").GetString().Should().Be("ok");
        anchored.GetProperty("startOffset").GetInt32().Should().Be(2);
        anchored.GetProperty("note").GetString().Should().Be("keep note");
    }

    [Fact]
    public async Task HighlightCreate_RejectsStaleRevisionAndPreservesExistingNote()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync();
        var detailPath = $"/api/articles/{article.Id}";
        var existing = await client.PostAsJsonAsync(detailPath + "/highlights", new
        {
            text = "readable", note = "keep note", startOffset = 0, endOffset = 8
        });
        existing.StatusCode.Should().Be(HttpStatusCode.OK);
        var replaced = await client.PostAsJsonAsync("/api/articles/content", new
        {
            url = article.Url.Value, title = "Replacement", originalContent = "new body", readableContent = "new body"
        });
        replaced.StatusCode.Should().Be(HttpStatusCode.Created);

        var stale = await client.PostAsJsonAsync(detailPath + "/highlights", new
        {
            text = "readable", note = "new note", startOffset = 0, endOffset = 8, article.ContentRevision
        });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var revision = (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current")
            .GetProperty("contentRevision").GetInt32();
        revision.Should().Be(article.ContentRevision + 1);
        var detail = await client.GetFromJsonAsync<JsonElement>(detailPath);
        var highlights = detail.GetProperty("highlights");
        highlights.GetArrayLength().Should().Be(1);
        highlights[0].GetProperty("note").GetString().Should().Be("keep note");
        highlights[0].GetProperty("anchorStatus").GetString().Should().Be("pending");

        var fresh = await client.PostAsJsonAsync(detailPath + "/highlights", new
        {
            text = "new body", note = "new note", startOffset = 0, endOffset = 8, contentRevision = revision
        });
        fresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var acknowledged = await fresh.Content.ReadFromJsonAsync<JsonElement>();
        acknowledged.GetProperty("note").GetString().Should().Be("new note");
        acknowledged.GetProperty("anchorStatus").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task ReaderPreferences_RealHttpRequiresLoginValidatesFullObjectAndPersists()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        const string path = "/api/account/reader-preferences";
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var login = await client.PostAsync("/api/account/login", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["userName"]="mini_cc", ["password"]="mini_cc_password", ["rememberMe"]="false" }));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var original = await client.GetFromJsonAsync<JsonElement>(path);
        try
        {
            foreach (var body in new[] { "{}", "{\"fontSize\":24}", "{\"fontSize\":29,\"lineHeight\":1.8,\"width\":\"standard\",\"fontFamily\":\"system\",\"theme\":\"light\"}", "{\"fontSize\":18,\"lineHeight\":2.3,\"width\":\"standard\",\"fontFamily\":\"system\",\"theme\":\"light\"}", "{\"fontSize\":18,\"lineHeight\":1.8,\"width\":\"invalid\",\"fontFamily\":\"system\",\"theme\":\"light\"}" })
                (await client.PutAsync(path, new StringContent(body, Encoding.UTF8, "application/json"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var preferences = new { fontSize=24, lineHeight=2.0, width="wide", fontFamily="serif", theme="sepia" };
            (await client.PutAsJsonAsync(path, preferences)).StatusCode.Should().Be(HttpStatusCode.OK);
            var saved = await client.GetFromJsonAsync<JsonElement>(path);
            saved.GetProperty("fontSize").GetInt32().Should().Be(24);
            saved.GetProperty("theme").GetString().Should().Be("sepia");
            saved.EnumerateObject().Should().HaveCount(5);
        }
        finally { (await client.PutAsJsonAsync(path, original)).StatusCode.Should().Be(HttpStatusCode.OK); }
    }

    [Fact]
    public async Task BatchAndEmptyTrash_ExposeValidationPartialResultsAndPreserveActiveArticles()
    {
        using var factory = CreateFactory();
        using var client = AuthenticatedClient(factory);
        var active = await fixture.SeedArticleAsync();
        var trash = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        (await client.PostAsJsonAsync("/api/articles/batch", new { ids = Array.Empty<Guid>(), action = "trash" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/articles/batch", new { ids = new[] { active.Id }, action = "unknown" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/articles/batch", new { ids = new[] { active.Id }, action = "addTags", tagIds = new[] { Guid.NewGuid() } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var response = await client.PostAsJsonAsync("/api/articles/batch", new { ids = new[] { active.Id, trash.Id }, action = "purge" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("succeeded")[0].GetGuid().Should().Be(trash.Id);
        result.GetProperty("failed")[0].GetProperty("reason").GetString().Should().Be("notInTrash");
        var moreTrash = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        var empty = await client.DeleteAsync("/api/trash");
        empty.StatusCode.Should().Be(HttpStatusCode.OK);
        (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("purged").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        await using var db = fixture.CreateContext();
        (await db.Articles.AnyAsync(a => a.Id == active.Id)).Should().BeTrue();
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == moreTrash.Id)).Should().BeFalse();
    }

    private async Task WaitForArticleLockAsync()
    {
        await using var monitor = fixture.CreateContext();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var waiting = await monitor.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%Articles%') AS "Value"
                """).SingleAsync();
            if (waiting) return;
            await Task.Delay(20);
        }

        throw new TimeoutException("The save did not reach the held PostgreSQL article lock.");
    }

    private WebApplicationFactory<ArticlesController> CreateFactory() => new WebApplicationFactory<ArticlesController>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageProxy:SignatureKey"] = "test-signing-key",
                ["Library:TrashRetentionDays"] = "45",
                ["Library:UndoWindowSeconds"] = "12"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<MiniCcDbContext>>();
                services.RemoveAll<MiniCcDbContext>();
                services.AddDbContext<MiniCcDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
                var keys = new Mock<IApiKeyService>();
                keys.Setup(k => k.ValidateAsync("test-api-key")).ReturnsAsync(true);
                services.RemoveAll<IApiKeyService>();
                services.AddSingleton(keys.Object);
            });
        });

    private static HttpClient AuthenticatedClient(WebApplicationFactory<ArticlesController> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key");
        return client;
    }
}
