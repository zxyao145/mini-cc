using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniCc.Api.Core.ApiKeys.Application.Services;
using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public class SqliteLibraryTests(SqliteFixture fixture)
{
    [Fact]
    public async Task Search_MatchesTitleSummaryAuthorAndBodySubstrings()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var tag = NewTag();
        var token = "检索" + Guid.NewGuid().ToString("N")[..6];
        var title = NewArticle(tag, title: $"前文{token}后文");
        var summary = NewArticle(tag, summary: $"前文{token}后文");
        var author = NewArticle(tag, author: $"前文{token}后文");
        var body = NewArticle(tag, readable: $"<p>前文{token}后文</p>");
        body.MarkRead(DateTimeOffset.UtcNow);
        await SaveArticlesAsync(title, summary, author, body, NewArticle(tag), NewArticle(NewTag(), title: token));

        var expected = new[] { title.Id, summary.Id, author.Id, body.Id };
        foreach (var term in new[] { token, token[1..5] })
            AssertMatches(await ListAsync(client, $"tags={tag.Id}&q={Uri.EscapeDataString(term)}"), expected);

        AssertMatches(await ListAsync(client, $"tags={tag.Id}&status=read&q={Uri.EscapeDataString(token)}"), body.Id);
    }

    [Fact]
    public async Task Search_TreatsWildcardsAndBackslashLiterallyAndIgnoresAsciiCase()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var tag = NewTag();
        var product = NewArticle(tag, title: "Product%_Name");
        var file = NewArticle(tag, title: @"C:\temp\file");
        await SaveArticlesAsync(product, NewArticle(tag, title: "ProductABName"), file, NewArticle(tag, title: "C:tempfile"));

        foreach (var term in new[] { "Product%_Name", "product%_name" })
            AssertMatches(await ListAsync(client, $"tags={tag.Id}&q={Uri.EscapeDataString(term)}"), product.Id);

        AssertMatches(await ListAsync(client, $"tags={tag.Id}&q={Uri.EscapeDataString(@"C:\temp")}"), file.Id);
    }

    [Fact]
    public async Task List_PagesEverySortAcrossNullsAndPages()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var tag = NewTag();
        var articles = Enumerable.Range(0, 5).Select(_ => NewArticle(tag)).ToArray();
        await SaveArticlesAsync(articles);

        var origin = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = articles.Select((a, i) => new
        {
            a.Id,
            CreatedAt = origin.AddMinutes(i),
            LastReadAt = i < 3 ? origin.AddMinutes(i % 2) : (DateTimeOffset?)null,
            PublishedAt = i < 2 ? origin.AddDays(-1) : (DateTimeOffset?)null,
            DeletedAt = i < 3 ? origin.AddDays(i + 1) : (DateTimeOffset?)null
        }).ToArray();

        await using var db = fixture.CreateContext();
        foreach (var row in rows)
            await db.Articles.Where(a => a.Id == row.Id).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.CreatedAt, row.CreatedAt)
                .SetProperty(a => a.LastReadAt, row.LastReadAt)
                .SetProperty(a => a.PublishedAt, row.PublishedAt));

        var sorts = new Dictionary<string, IEnumerable<Guid>>
        {
            ["saved_desc"] = rows.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Select(r => r.Id),
            ["saved_asc"] = rows.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => r.Id),
            ["read_desc"] = rows.OrderByDescending(r => r.LastReadAt).ThenByDescending(r => r.Id).Select(r => r.Id),
            ["published_desc"] = rows.OrderByDescending(r => r.PublishedAt).ThenByDescending(r => r.Id).Select(r => r.Id)
        };
        foreach (var (sort, expected) in sorts)
            (await PageIdsAsync(client, $"tags={tag.Id}&sort={sort}", rows.Length)).Should().Equal(expected);

        var trash = rows.Where(r => r.DeletedAt.HasValue).OrderByDescending(r => r.DeletedAt).ToList();
        foreach (var row in trash)
            await db.Articles.Where(a => a.Id == row.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.DeletedAt, row.DeletedAt));

        (await PageIdsAsync(client, $"tags={tag.Id}&view=trash", trash.Count)).Should().Equal(trash.Select(r => r.Id));
    }

    [Fact]
    public async Task List_DateRangeUsesInstantsAcrossOffsets()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var tag = NewTag();
        var article = NewArticle(tag);
        await SaveArticlesAsync(article);
        var createdAt = new DateTimeOffset(2026, 11, 14, 16, 0, 0, TimeSpan.Zero);
        await using var db = fixture.CreateContext();
        await db.Articles.Where(a => a.Id == article.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAt, createdAt));

        const string boundary = "2026-11-15T00:00:00%2B08:00";
        AssertMatches(await ListAsync(client, $"tags={tag.Id}&savedFrom={boundary}"), article.Id);
        AssertMatches(await ListAsync(client, $"tags={tag.Id}&savedTo={boundary}"));
        AssertMatches(await ListAsync(client, $"tags={tag.Id}&savedTo=2026-11-15T00:00:00.0000001%2B08:00"), article.Id);
    }

    [Fact]
    public async Task List_ProjectsEachFeedOnce()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var tag = NewTag();
        var article = NewArticle(tag);
        await SaveArticlesAsync(article);
        var a = NewFeed("Feed A");
        var b = NewFeed("Feed B");
        await using var db = fixture.CreateContext();
        db.Feeds.AddRange(a, b);
        db.FeedEntries.AddRange(Entry(a, article, "a1"), Entry(a, article, "a2"), Entry(b, article, "b1"));
        await db.SaveChangesAsync();

        var item = (await ListAsync(client, $"tags={tag.Id}")).GetProperty("items").EnumerateArray().Single();
        var feeds = item.GetProperty("feeds").EnumerateArray().Select(f => f.GetProperty("title").GetString());
        feeds.Should().BeEquivalentTo(["Feed A", "Feed B"]);
    }

    [Fact]
    public async Task ApiKeyValidation_HonorsExpiryOnSqlite()
    {
        await using var db = fixture.CreateContext();
        var user = await db.Users.SingleAsync();
        var expired = new ApiKey { Id = Guid.NewGuid(), UserId = user.Id, Name = "Expired", Key = "expired-test-key", ExpiredTime = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var active = new ApiKey { Id = Guid.NewGuid(), UserId = user.Id, Name = "Active", Key = "active-test-key", ExpiredTime = DateTimeOffset.UtcNow.AddDays(1) };
        db.ApiKeys.AddRange(expired, active);
        await db.SaveChangesAsync();
        var service = new ApiKeyService(db, NullLogger<ApiKeyService>.Instance);

        (await service.ValidateAsync(expired.Key)).Should().BeFalse();
        (await service.ValidateAsync(active.Key)).Should().BeTrue();
    }

    [Fact]
    public async Task FeedTrashArticles_TrashesEligibleArticles()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var article = await fixture.SeedArticleAsync();
        var feed = NewFeed("Feed to trash");
        await using var db = fixture.CreateContext();
        db.Feeds.Add(feed);
        db.FeedEntries.Add(Entry(feed, article, "eligible"));
        await db.SaveChangesAsync();

        using var response = await client.PostAsync($"/api/feeds/{feed.Id}/trash-articles", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("trashed").GetInt32().Should().Be(1);
        result.GetProperty("skipped").GetInt32().Should().Be(0);
        var deleted = await db.Articles.IgnoreQueryFilters().Where(a => a.Id == article.Id).Select(a => a.DeletedAt).SingleAsync();
        deleted.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TagNameConflict_IsRecognizedOnSqlite()
    {
        var tag = NewTag();
        await using (var first = fixture.CreateContext())
        {
            first.Tags.Add(tag);
            await first.SaveChangesAsync();
        }

        await using var second = fixture.CreateContext();
        second.Tags.Add(Tag.Create(tag.Name, TagColor.Create("#112233")));
        Func<Task> save = () => second.SaveChangesAsync();
        var exception = (await save.Should().ThrowAsync<DbUpdateException>()).Which;
        DatabaseErrors.IsUniqueViolation(exception, "Tags", "IX_Tags_Name", "Name").Should().BeTrue();
        DatabaseErrors.IsUniqueViolation(exception, "Users", "IX_Users_UserName", "UserName").Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentTagCreates_SameName_ReturnCreatedAndConflict()
    {
        using var factory = SqliteApi.CreateFactory(fixture);
        using var client = SqliteApi.AuthenticatedClient(factory);
        var suffix = Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/tags", new { name = $"dup-{suffix}" }),
            client.PostAsJsonAsync("/api/tags", new { name = $"dup-{suffix}" }));
        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        var created = await responses.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<JsonElement>();
        var conflict = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).Content.ReadFromJsonAsync<JsonElement>();
        conflict.GetProperty("existingTagId").GetGuid().Should().Be(created.GetProperty("id").GetGuid());

        (await client.PostAsJsonAsync("/api/tags", new { name = $"Case-{suffix}" })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/api/tags", new { name = $"case-{suffix}" })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static Task<JsonElement> ListAsync(HttpClient client, string query) => client.GetFromJsonAsync<JsonElement>($"/api/articles?{query}");

    private static IEnumerable<Guid> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid());

    private static void AssertMatches(JsonElement page, params Guid[] expected)
    {
        page.GetProperty("total").GetInt32().Should().Be(expected.Length);
        Ids(page).Should().BeEquivalentTo(expected);
    }

    private static async Task<List<Guid>> PageIdsAsync(HttpClient client, string query, int total)
    {
        var ids = new List<Guid>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < (total + 1) / 2; pageNumber++)
        {
            var pageQuery = $"{query}&pageSize=2";
            if (cursor != null) pageQuery += $"&cursor={Uri.EscapeDataString(cursor)}";
            var page = await ListAsync(client, pageQuery);
            page.GetProperty("total").GetInt32().Should().Be(total);
            ids.AddRange(Ids(page));
            cursor = page.GetProperty("nextCursor").GetString();
            if (cursor == null) break;
        }

        cursor.Should().BeNull();
        return ids;
    }

    private static Tag NewTag() => Tag.Create($"tag-{Guid.NewGuid():N}", TagColor.Create("#112233"));

    private static Article NewArticle(Tag tag, string title = "Title", string summary = "", string author = "", string readable = "<p>Body</p>")
    {
        var article = Article.Create(Url.Create($"https://example.com/{Guid.NewGuid():N}"), title, author, Content.Create(readable, readable), summary);
        article.AddTag(tag);
        return article;
    }

    private async Task SaveArticlesAsync(params Article[] articles)
    {
        await using var db = fixture.CreateContext();
        db.Articles.AddRange(articles);
        await db.SaveChangesAsync();
    }

    private static Feed NewFeed(string title)
    {
        var feed = Feed.Create($"https://example.com/{Guid.NewGuid():N}", title, null, FeedFormat.Rss);
        feed.NextFetchAt = DateTimeOffset.UtcNow.AddDays(1);
        return feed;
    }

    private static FeedEntry Entry(Feed feed, Article article, string key)
    {
        var entry = FeedEntry.Create(feed.Id, key, article.Url.Value, "hash");
        entry.ArticleId = article.Id;
        return entry;
    }
}
