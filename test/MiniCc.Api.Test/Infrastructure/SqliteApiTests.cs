using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Utils;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public class SqliteApiTests(SqliteFixture fixture)
{
    [Fact]
    public async Task Api_StartsOnSqlite()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);

        using var response = await client.GetAsync("/api/tags");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_RememberMeControlsCookiePersistenceAndLifetime(bool rememberMe)
    {
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var now = issuedAt;
        var clock = new Mock<TimeProvider>();
        clock.Setup(provider => provider.GetUtcNow()).Returns(() => now);
        using var baseFactory = SqliteApi.CreateFactory(fixture);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
                options => options.TimeProvider = clock.Object)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var cookie = await LoginAsync(client, rememberMe: rememberMe);
        cookie.Expires.Should().Be(rememberMe ? issuedAt.AddDays(30) : null);
        cookie.MaxAge.Should().BeNull();

        UseCookie(client, cookie);
        var current = await client.GetFromJsonAsync<JsonElement>("/api/account/current");
        current.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();

        now = issuedAt.AddMinutes(31);
        current = await client.GetFromJsonAsync<JsonElement>("/api/account/current");
        current.GetProperty("isAuthenticated").GetBoolean().Should().Be(rememberMe);

        SetCookieHeaderValue? renewedCookie = null;
        if (rememberMe)
        {
            now = issuedAt.AddDays(16);
            using var renewal = await client.GetAsync("/api/account/current");
            renewal.EnsureSuccessStatusCode();
            renewedCookie = ReadCookie(renewal);
            renewedCookie.Name.Should().Be(cookie.Name);
            renewedCookie.Value.Should().NotBe(cookie.Value);
            renewedCookie.Expires.Should().Be(now.AddDays(30));
        }

        now = issuedAt.AddDays(30).AddSeconds(1);
        current = await client.GetFromJsonAsync<JsonElement>("/api/account/current");
        current.GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();

        if (renewedCookie is not null)
        {
            UseCookie(client, renewedCookie);
            current = await client.GetFromJsonAsync<JsonElement>("/api/account/current");
            current.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdatePassword_RevokesExistingCookiesAndKeepsCurrentBrowserSignedIn(bool rememberMe)
    {
        const string oldPassword = "before-password-change";
        const string newPassword = "after-password-change";
        var userName = $"password-{Guid.NewGuid():N}";
        await using (var db = fixture.CreateContext())
        {
            db.Users.Add(User.Create(userName, PasswordUtil.HashPassword(oldPassword)));
            await db.SaveChangesAsync();
        }

        using var factory = SqliteApi.CreateFactory(fixture);
        using var changingClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var otherClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var changingCookie = await LoginAsync(changingClient, userName, oldPassword, rememberMe);
        var otherCookie = await LoginAsync(otherClient, userName, oldPassword, rememberMe: true);
        UseCookie(changingClient, changingCookie);
        UseCookie(otherClient, otherCookie);
        using var previouslyAuthorized = await otherClient.GetAsync("/api/account/reader-preferences");
        previouslyAuthorized.StatusCode.Should().Be(HttpStatusCode.OK);

        using var changed = await changingClient.PutAsJsonAsync("/api/account/password",
            new { currentPassword = oldPassword, newPassword });
        changed.StatusCode.Should().Be(HttpStatusCode.OK);

        using var rejected = await otherClient.GetAsync("/api/account/reader-preferences");
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var rejectedChangingCookie = await changingClient.GetAsync("/api/account/reader-preferences");
        rejectedChangingCookie.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var replacement = ReadCookie(changed);
        replacement.Expires.HasValue.Should().Be(rememberMe);
        UseCookie(changingClient, replacement);
        using var current = await changingClient.GetAsync("/api/account/reader-preferences");
        current.StatusCode.Should().Be(HttpStatusCode.OK);

        otherClient.DefaultRequestHeaders.Remove("Cookie");
        UseCookie(otherClient, await LoginAsync(otherClient, userName, newPassword, rememberMe: true));
        using var signedInAgain = await otherClient.GetAsync("/api/account/reader-preferences");
        signedInAgain.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RememberMeCookie_SurvivesApiRestartWithPersistedKeys()
    {
        var keys = Directory.CreateTempSubdirectory("minicc-cookie-keys-");
        try
        {
            using var baseFactory = SqliteApi.CreateFactory(fixture);
            SetCookieHeaderValue cookie;
            using (var firstHost = baseFactory.WithWebHostBuilder(ConfigureKeys))
            using (var client = firstHost.CreateClient())
            {
                cookie = await LoginAsync(client, rememberMe: true);
                keys.GetFiles("key-*.xml").Should().NotBeEmpty();
            }

            using var secondHost = baseFactory.WithWebHostBuilder(ConfigureKeys);
            using var restarted = secondHost.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
            UseCookie(restarted, cookie);
            using var response = await restarted.GetAsync("/api/account/reader-preferences");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            keys.Delete(recursive: true);
        }

        void ConfigureKeys(IWebHostBuilder builder) => builder.UseSetting("DataProtection:KeysPath", keys.FullName);
    }

    [Fact]
    public async Task ArticleLifecycle_WorksOnSqlite()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client);

        var token = Guid.NewGuid().ToString("N");
        var url = $"https://example.com/{token}";
        var title = $"Saved article {token}";
        var saved = await SaveContentAsync(client, url, title, "<p>Readable text with enough characters.</p>");
        var articleId = saved.GetProperty("id").GetGuid();
        var revision = saved.GetProperty("contentRevision").GetInt32();
        var path = $"/api/articles/{articleId}";

        var link = await JsonAsync(client.PostAsJsonAsync("/api/articles/link", new { url = $"{url}/link", title = "Saved link" }), HttpStatusCode.Created);
        link.GetProperty("contentKind").GetString().Should().Be("link");
        var linkId = link.GetProperty("id").GetGuid();

        var detail = await client.GetFromJsonAsync<JsonElement>(path);
        detail.GetProperty("title").GetString().Should().Be(title);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=all&status=unread&source=extension&q={token}");
        list.GetProperty("total").GetInt32().Should().Be(1);
        list.GetProperty("items")[0].GetProperty("id").GetGuid().Should().Be(articleId);

        var state = await JsonAsync(client.PatchAsJsonAsync($"{path}/state", new { isFavorite = true, isArchived = true, readingStatus = "read" }), HttpStatusCode.OK);
        state.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        state.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        state.GetProperty("readingStatus").GetString().Should().Be("read");

        var first = await JsonAsync(SaveProgressAsync(client, path, "session-a", null, revision), HttpStatusCode.OK);
        var version = first.GetProperty("progressUpdatedAt").GetString();
        var second = await JsonAsync(SaveProgressAsync(client, path, "session-b", version, revision), HttpStatusCode.OK);
        var firstTime = first.GetProperty("progressUpdatedAt").GetDateTimeOffset();
        var secondTime = second.GetProperty("progressUpdatedAt").GetDateTimeOffset();
        secondTime.Should().BeAfter(firstTime);
        (firstTime.UtcTicks % 10).Should().Be(0);
        (secondTime.UtcTicks % 10).Should().Be(0);
        var conflict = await JsonAsync(SaveProgressAsync(client, path, "session-c", null, revision), HttpStatusCode.Conflict);
        conflict.GetProperty("current").GetProperty("progressUpdatedAt").GetDateTimeOffset().Should().Be(secondTime);
        await JsonAsync(client.PostAsJsonAsync($"{path}/progress/reset", new { sessionId = "session-b", contentRevision = revision }), HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>(path)).GetProperty("progress").ValueKind.Should().Be(JsonValueKind.Null);

        var highlight = await JsonAsync(client.PostAsJsonAsync($"{path}/highlights",
            new { text = "Readable", note = "Persisted note", startOffset = 0, endOffset = 8, contentRevision = revision }), HttpStatusCode.OK);
        var highlightId = highlight.GetProperty("id").GetGuid();
        var revised = await SaveContentAsync(client, url, title, "<p>New Readable text with enough characters.</p>");
        revised.GetProperty("id").GetGuid().Should().Be(articleId);
        revised.GetProperty("highlights")[0].GetProperty("anchorStatus").GetString().Should().Be("pending");
        var anchored = await JsonAsync(client.PutAsJsonAsync($"/api/highlights/{highlightId}/anchor", new
        {
            startOffset = 4,
            endOffset = 12,
            contentRevision = revised.GetProperty("contentRevision").GetInt32()
        }), HttpStatusCode.OK);
        anchored.GetProperty("anchorStatus").GetString().Should().Be("ok");
        anchored.GetProperty("startOffset").GetInt32().Should().Be(4);

        (await client.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var restored = await JsonAsync(client.PostAsync($"{path}/restore", null), HttpStatusCode.OK);
        restored.GetProperty("isFavorite").GetBoolean().Should().BeTrue();
        restored.GetProperty("isArchived").GetBoolean().Should().BeTrue();

        var tag = await JsonAsync(client.PostAsJsonAsync("/api/tags", new { name = $"Shared {token}" }), HttpStatusCode.Created);
        var tagId = tag.GetProperty("id").GetGuid();
        await AssertBatchAsync(client, "addTags", [articleId], tagId);
        await AssertBatchAsync(client, "removeTags", [articleId], tagId);
        (await client.GetFromJsonAsync<JsonElement>(path)).GetProperty("tags").GetArrayLength().Should().Be(0);
        await AssertBatchAsync(client, "addTags", [articleId, linkId], tagId);
        await AssertBatchAsync(client, "trash", [articleId]);
        await AssertBatchAsync(client, "restore", [articleId]);
        await AssertBatchAsync(client, "trash", [articleId]);
        await AssertBatchAsync(client, "purge", [articleId]);

        await using var db = fixture.CreateContext();
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == articleId)).Should().BeFalse();
        (await db.Highlights.IgnoreQueryFilters().AnyAsync(h => h.ArticleId == articleId)).Should().BeFalse();
        (await db.Tags.AnyAsync(t => t.Id == tagId)).Should().BeTrue();
        (await db.Articles.AnyAsync(a => a.Id == linkId && a.Tags.Any(t => t.Id == tagId))).Should().BeTrue();

        await AssertBatchAsync(client, "trash", [linkId]);
        var empty = await JsonAsync(client.DeleteAsync("/api/trash"), HttpStatusCode.OK);
        empty.GetProperty("purged").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == linkId)).Should().BeFalse();
    }

    [Fact]
    public async Task TagLifecycle_WorksOnSqlite()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var suffix = Guid.NewGuid().ToString("N");
        var tagPath = "/api/tags";
        var tag = await JsonAsync(client.PostAsJsonAsync(tagPath, new { name = $"Create {suffix}", color = "#112233" }), HttpStatusCode.Created);
        var tagId = tag.GetProperty("id").GetGuid();
        var newName = $"Rename {suffix}";
        (await client.PutAsJsonAsync($"{tagPath}/{tagId}", new { name = newName, color = "#445566" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        var matches = await client.GetFromJsonAsync<JsonElement>($"{tagPath}?search={Uri.EscapeDataString(newName)}");
        var match = matches.EnumerateArray().Single();
        match.GetProperty("id").GetGuid().Should().Be(tagId);
        match.GetProperty("color").GetString().Should().Be("#445566");

        var article = await fixture.SeedArticleAsync();
        var articleTags = $"/api/articles/{article.Id}/tags";
        var attached = await JsonAsync(client.PostAsJsonAsync(articleTags, new { name = newName }), HttpStatusCode.OK);
        attached.GetProperty("id").GetGuid().Should().Be(tagId);
        (await client.DeleteAsync($"{articleTags}/{tagId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<JsonElement>($"/api/articles/{article.Id}")).GetProperty("tags").GetArrayLength().Should().Be(0);
        await JsonAsync(client.PostAsJsonAsync(articleTags, new { name = newName }), HttpStatusCode.OK);
        var feed = Feed.Create($"https://example.com/feed/{suffix}", "Tagged feed", null, FeedFormat.Rss);
        feed.NextFetchAt = DateTimeOffset.UtcNow.AddDays(1);
        await using (var seed = fixture.CreateContext())
        {
            feed.Tags.Add(await seed.Tags.SingleAsync(t => t.Id == tagId));
            seed.Feeds.Add(feed);
            await seed.SaveChangesAsync();
        }

        (await client.DeleteAsync($"{tagPath}/{tagId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using var db = fixture.CreateContext();
        (await db.Articles.Include(a => a.Tags).SingleAsync(a => a.Id == article.Id)).Tags.Should().BeEmpty();
        (await db.Feeds.Include(f => f.Tags).SingleAsync(f => f.Id == feed.Id)).Tags.Should().BeEmpty();
        (await db.Set<Dictionary<string, object>>("ArticleTags").AnyAsync(j => EF.Property<Guid>(j, "TagsId") == tagId)).Should().BeFalse();
        (await db.Set<Dictionary<string, object>>("FeedTags").AnyAsync(j => EF.Property<Guid>(j, "TagId") == tagId)).Should().BeFalse();
    }

    [Fact]
    public async Task FeedLifecycle_WorksOnSqlite()
    {
        var articleUrl = $"https://example.com/{Guid.NewGuid():N}";
        using var baseFactory = SqliteApi.CreateFactory(fixture);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHttpClient("feeds").ConfigurePrimaryHttpMessageHandler(() => new LibraryApiTests.FeedHttpHandler(articleUrl))));
        using var client = SqliteApi.AuthenticatedClient(factory);
        var url = $"{articleUrl}/feed";
        var preview = await JsonAsync(client.PostAsJsonAsync("/api/feeds/preview", new { url }), HttpStatusCode.OK);
        preview.GetProperty("items").GetArrayLength().Should().Be(1);
        var created = await JsonAsync(client.PostAsJsonAsync("/api/feeds", new { feedUrl = url, title = "Named feed", tagIds = Array.Empty<Guid>() }), HttpStatusCode.Created);
        created.GetProperty("imported").GetInt32().Should().Be(1);
        var feedId = created.GetProperty("feed").GetProperty("id").GetGuid();
        var path = $"/api/feeds/{feedId}";
        var feeds = await client.GetFromJsonAsync<JsonElement>("/api/feeds");
        feeds.EnumerateArray().Should().Contain(f => f.GetProperty("id").GetGuid() == feedId);

        var tag = await JsonAsync(client.PostAsJsonAsync("/api/tags", new { name = $"Feed tag {Guid.NewGuid():N}" }), HttpStatusCode.Created);
        var tagId = tag.GetProperty("id").GetGuid();
        var paused = await JsonAsync(client.PatchAsJsonAsync(path, new { title = "Renamed feed", tagIds = new[] { tagId }, paused = true }), HttpStatusCode.OK);
        paused.GetProperty("status").GetString().Should().Be("paused");
        paused.GetProperty("title").GetString().Should().Be("Renamed feed");
        paused.GetProperty("tags")[0].GetProperty("id").GetGuid().Should().Be(tagId);
        (await client.PostAsync($"{path}/refresh", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await JsonAsync(client.PatchAsJsonAsync(path, new { paused = false }), HttpStatusCode.OK);
        var refreshed = await JsonAsync(client.PostAsync($"{path}/refresh", null), HttpStatusCode.OK);
        refreshed.GetProperty("status").GetString().Should().Be("ok");

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/articles?view=feeds&feedId={feedId}");
        list.GetProperty("total").GetInt32().Should().Be(1);
        list.GetProperty("items")[0].GetProperty("feeds")[0].GetProperty("id").GetGuid().Should().Be(feedId);

        (await client.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        feeds = await client.GetFromJsonAsync<JsonElement>("/api/feeds");
        feeds.EnumerateArray().Should().NotContain(f => f.GetProperty("id").GetGuid() == feedId);
    }

    [Fact]
    public async Task ReaderPreferences_RoundTripOnSqlite()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client);
        const string path = "/api/account/reader-preferences";
        var original = await client.GetFromJsonAsync<JsonElement>(path);
        try
        {
            var prefs = new { fontSize = 28, lineHeight = 2.2, width = "wide", fontFamily = "serif", theme = "sepia" };
            await JsonAsync(client.PutAsJsonAsync(path, prefs), HttpStatusCode.OK);

            var saved = await client.GetFromJsonAsync<JsonElement>(path);
            saved.GetProperty("fontSize").GetInt32().Should().Be(prefs.fontSize);
            saved.GetProperty("lineHeight").GetDouble().Should().Be(prefs.lineHeight);
            saved.GetProperty("width").GetString().Should().Be(prefs.width);
            saved.GetProperty("fontFamily").GetString().Should().Be(prefs.fontFamily);
            saved.GetProperty("theme").GetString().Should().Be(prefs.theme);
        }
        finally
        {
            (await client.PutAsJsonAsync(path, original)).EnsureSuccessStatusCode();
        }
    }

    private static async Task<SetCookieHeaderValue> LoginAsync(HttpClient client, string userName = "mini_cc",
        string password = "mini_cc_password", bool rememberMe = false)
    {
        using var credentials = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = userName,
            ["password"] = password,
            ["rememberMe"] = rememberMe ? "true" : "false"
        });
        using var response = await client.PostAsync("/api/account/login", credentials);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return ReadCookie(response);
    }

    private static SetCookieHeaderValue ReadCookie(HttpResponseMessage response) =>
        SetCookieHeaderValue.Parse(Assert.Single(response.Headers.GetValues("Set-Cookie")));

    private static void UseCookie(HttpClient client, SetCookieHeaderValue cookie)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"{cookie.Name}={cookie.Value}");
    }

    private static Task<JsonElement> SaveContentAsync(HttpClient client, string url, string title, string body) =>
        JsonAsync(client.PostAsJsonAsync("/api/articles/content", new { url, title, originalContent = body, readableContent = body }), HttpStatusCode.Created);

    private static Task<HttpResponseMessage> SaveProgressAsync(HttpClient client, string path, string sessionId, string? baseUpdatedAt, int revision) =>
        client.PutAsJsonAsync($"{path}/progress",
            new { sessionId, baseUpdatedAt, contentRevision = revision, textOffset = 4, blockIndex = 0, blockRatio = .5, percent = 42 });

    private static async Task AssertBatchAsync(HttpClient client, string action, Guid[] ids, params Guid[] tagIds)
    {
        var result = await JsonAsync(client.PostAsJsonAsync("/api/articles/batch", new { ids, action, tagIds }), HttpStatusCode.OK);
        result.GetProperty("succeeded").EnumerateArray().Select(id => id.GetGuid()).Should().BeEquivalentTo(ids);
        result.GetProperty("failed").GetArrayLength().Should().Be(0);
    }

    private static async Task<JsonElement> JsonAsync(Task<HttpResponseMessage> request, HttpStatusCode status)
    {
        using var response = await request;
        response.StatusCode.Should().Be(status);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
