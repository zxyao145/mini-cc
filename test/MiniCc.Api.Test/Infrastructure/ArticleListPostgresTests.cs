using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class ArticleListPostgresTests(PostgresFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var db = fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Articles\", \"Tags\" CASCADE");
    }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<ArticleListPage> List(ArticleListRequest request)
    {
        await using var db = fixture.CreateContext();
        var result = await new GetArticleListQueryHandler(db, new ImageProxyService(Options.Create(new ImageProxyOptions())))
            .Handle(new GetArticleListQuery(request), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Value;
    }

    [Theory]
    [InlineData("saved_desc")]
    [InlineData("saved_asc")]
    [InlineData("read_desc")]
    [InlineData("published_desc")]
    public async Task Keyset_65ItemsIncludingTiesAndNulls_AreReturnedExactlyOnce(string sort)
    {
        await using var db = fixture.CreateContext();
        var time = DateTimeOffset.UtcNow.AddDays(-2);
        for (var i = 0; i < 65; i++)
        {
            var article = await fixture.SeedArticleAsync();
            var key = i % 3 == 0 ? (DateTimeOffset?)null : time.AddMinutes(i / 4);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"CreatedAt\" = {time}, \"LastReadAt\" = {key}, \"PublishedAt\" = {key} WHERE \"Id\" = {article.Id}");
        }
        var ids = new List<Guid>();
        var rows = new List<ArticleListItemDto>();
        string? cursor = null;
        do
        {
            var page = await List(new() { View = "all", Sort = sort, Cursor = cursor });
            page.Total.Should().Be(65);
            ids.AddRange(page.Items.Select(a => a.Id));
            rows.AddRange(page.Items);
            cursor = page.NextCursor;
        } while (cursor != null);
        ids.Should().HaveCount(65).And.OnlyHaveUniqueItems();
        if (sort == "read_desc") rows.Select(a => a.LastReadAt).Should().BeInDescendingOrder();
        if (sort == "published_desc") rows.Select(a => a.PublishedAt).Should().BeInDescendingOrder();
        ids.Should().BeEquivalentTo(await db.Articles.Select(a => a.Id).ToListAsync());
    }

    [Fact]
    public async Task Keyset_DeleteLoadedItemBetweenPages_DoesNotSkipRemainingItems()
    {
        for (var i = 0; i < 25; i++) await fixture.SeedArticleAsync();
        var first = await List(new() { View = "all" });
        await using var db = fixture.CreateContext();
        var deleted = await db.Articles.FindAsync(first.Items[0].Id);
        deleted!.MoveToTrash(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        var second = await List(new() { View = "all", Cursor = first.NextCursor });
        second.Total.Should().Be(24);
        first.Items.Concat(second.Items).Select(a => a.Id).Should().HaveCount(25).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CombinedFilters_ChineseSummaryTitleFallbackAndLiteralWildcards()
    {
        var tag = Tag.Create(".NET", TagColor.Create("#112233"));
        var second = Tag.Create("cache", TagColor.Create("#223344"));
        var article = await fixture.SeedArticleAsync(a => { a.UpdateMetadata("Product%_Name", "人工智能缓存"); a.AddTag(tag); a.AddTag(second); a.SetFavorite(true); a.MarkSavedFromExtension(DateTimeOffset.UtcNow); });
        await fixture.SeedArticleAsync(a => { a.UpdateMetadata("缓存", ""); a.SetArchived(true); });
        await using (var seed = fixture.CreateContext())
        {
            var tags = await seed.Tags.ToListAsync();
            foreach (var variant in new[] { "archived", "read", "one-tag", "wildcard-distractor" })
            {
                var other = Article.Create(Url.Create($"https://example.com/{variant}"), "ProductABName", "", Content.Create("original", "readable"), "人工智能缓存");
                other.AddTag(tags.Single(t => t.Id == tag.Id));
                if (variant != "one-tag") other.AddTag(tags.Single(t => t.Id == second.Id));
                other.MarkSavedFromExtension(DateTimeOffset.UtcNow);
                if (variant == "archived") other.SetArchived(true);
                if (variant == "read") other.MarkRead(DateTimeOffset.UtcNow);
                if (variant == "wildcard-distractor") other.UpdateMetadata("ProductABName", "different summary");
                seed.Articles.Add(other);
            }
            await seed.SaveChangesAsync();
        }
        var request = new ArticleListRequest { View = "inbox", Status = "unread", Tags = $"{tag.Id},{second.Id}", Q = "人工智能", Source = "extension", SavedFrom = DateTimeOffset.UtcNow.AddDays(-1), SavedTo = DateTimeOffset.UtcNow.AddDays(1), PageSize = 1 };
        var page = await List(request);
        page.Total.Should().Be(1);
        page.Items.Single().Id.Should().Be(article.Id);
        page.Items.Single().SavedFromExtension.Should().BeTrue();
        request.Q = "%_";
        (await List(request)).Total.Should().Be(1);
        request.Q = "notpresent";
        (await List(request)).Total.Should().Be(0);
        (await List(new() { View = "favorites" })).Total.Should().Be(1);
        request.Q = null;
        request.Source = "feed";
        (await List(request)).Total.Should().Be(0);
    }

    [Fact]
    public async Task Trash_IgnoresRequestedSortAndIncludesAnnotationCountsWithoutContent()
    {
        await fixture.SeedArticleAsync();
        var article = await fixture.SeedArticleAsync(a => { a.AddHighlight(Highlight.Create(a.Id, "text", "secret", 0, 4)); a.MoveToTrash(DateTimeOffset.UtcNow); });
        var page = await List(new() { View = "trash", Sort = "saved_asc" });
        page.Total.Should().Be(1);
        page.Items.Single().Id.Should().Be(article.Id);
        page.Items.Single().HighlightCount.Should().Be(1);
        typeof(ArticleListItemDto).GetProperties().Select(p => p.Name).Should().NotContain(new[] { "ReadableContent", "OriginalContent", "Highlights" });
    }
}
