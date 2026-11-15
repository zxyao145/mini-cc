using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class ArticleListFeedProjectionPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task List_ProjectsEachFeedOnce()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var token = Guid.NewGuid().ToString("N");
        var article = Article.Create(Url.Create($"https://example.com/{token}"), token, "",
            Content.Create("original", "readable"));
        var feedA = Feed.Create($"https://example.com/{token}/a", "Feed A", null, FeedFormat.Rss);
        var feedB = Feed.Create($"https://example.com/{token}/b", "Feed B", null, FeedFormat.Rss);

        db.Articles.Add(article);
        db.Feeds.AddRange(feedA, feedB);
        db.FeedEntries.AddRange(Entry(feedA, "a1"), Entry(feedA, "a2"), Entry(feedB, "b1"));
        await db.SaveChangesAsync();

        var handler = new GetArticleListQueryHandler(db, new ImageProxyService(Options.Create(new ImageProxyOptions())));
        var request = new ArticleListRequest { View = "feeds", Q = token };
        var result = await handler.Handle(new GetArticleListQuery(request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Total.Should().Be(1);
        var item = result.Value.Items.Should().ContainSingle().Which;
        item.Id.Should().Be(article.Id);
        item.Feeds.Should().BeEquivalentTo(new[] { new ArticleFeedDto(feedA.Id, "Feed A"), new ArticleFeedDto(feedB.Id, "Feed B") });

        FeedEntry Entry(Feed feed, string key)
        {
            var entry = FeedEntry.Create(feed.Id, key, article.Url.Value, "hash");
            entry.ArticleId = article.Id;
            return entry;
        }
    }
}
