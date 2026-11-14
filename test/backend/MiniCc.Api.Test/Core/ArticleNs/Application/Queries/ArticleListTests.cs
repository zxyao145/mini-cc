using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Queries;

public class ArticleListTests
{
    [Fact]
    public void Cursor_RoundTripsNullableTimestampAndRejectsWrongSortOrMalformedInput()
    {
        var id = Guid.NewGuid();
        foreach (var value in new DateTimeOffset?[] { DateTimeOffset.UtcNow, null })
        {
            var encoded = ArticleCursor.Encode("read_desc", value, id);
            encoded.Should().NotContainAny("+", "/", "=");
            var decoded = ArticleCursor.Decode(encoded, "read_desc");
            decoded.Id.Should().Be(id);
            decoded.Value.Should().Be(value);
            ((Action)(() => ArticleCursor.Decode(encoded, "saved_desc"))).Should().Throw<ArgumentException>();
        }
        foreach (var input in new[] { "!", "e30", "bnVsbA", new string('a', 2000) })
            ((Action)(() => ArticleCursor.Decode(input, "saved_desc"))).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("unsupported", null, null, 20)]
    [InlineData("invalid", null, null, 20)]
    [InlineData("inbox", "Reading", null, 20)]
    [InlineData("inbox", null, "not-guid", 20)]
    [InlineData("inbox", null, null, 51)]
    public void Request_RejectsUnsupportedValues(string view, string? status, string? tags, int pageSize)
    {
        var request = new ArticleListRequest { View = view, Status = status, Tags = tags, PageSize = pageSize };
        ((Action)(() => request.Validate())).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Filter_ComposesViewStatusAllTagsSourceAndDateBounds()
    {
        var tag = Tag.Create("dotnet");
        var secondTag = Tag.Create("cache");
        Article Create(bool archived = false, bool read = false, bool bothTags = true)
        {
            var article = Article.Create(Url.Create("https://example.com"), "Title", "", Content.Create("original", "readable"));
            article.AddTag(tag);
            if (bothTags) article.AddTag(secondTag);
            article.SetArchived(archived);
            if (read) article.MarkRead(DateTimeOffset.UtcNow);
            article.MarkSavedFromExtension(DateTimeOffset.UtcNow);
            return article;
        }
        var expected = Create();
        var items = new[] { expected, Create(true), Create(read: true), Create(bothTags: false) }.AsQueryable();
        var request = new ArticleListRequest { Status = "unread,reading", Tags = $"{tag.Id},{secondTag.Id}", Source = "extension", SavedFrom = DateTimeOffset.UtcNow.AddDays(-1), SavedTo = DateTimeOffset.UtcNow.AddDays(1) };
        ArticleListFilter.Apply(items, request).Should().ContainSingle().Which.Should().Be(expected);
        request.SavedTo = expected.CreatedAt;
        ArticleListFilter.Apply(items, request).Should().BeEmpty();
    }
}
