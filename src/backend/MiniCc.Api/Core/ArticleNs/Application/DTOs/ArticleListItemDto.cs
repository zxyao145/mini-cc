using System.Linq.Expressions;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Application.DTOs;

namespace MiniCc.Api.Core.ArticleNs.Application.DTOs;

public class ArticleListItemDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Summary { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public ReadingStatus ReadingStatus { get; set; }
    public double? ProgressPercent { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public ContentKind ContentKind { get; set; }
    public bool SavedFromExtension { get; set; }
    public List<ArticleFeedDto> Feeds { get; set; } = [];
    public List<TagDto> Tags { get; set; } = [];
    public int HighlightCount { get; set; }

    public static readonly Expression<Func<Article, ArticleListItemDto>> Projection = a => new ArticleListItemDto
    {
        Id = a.Id, Url = a.Url.Value, Title = a.Title, Author = a.Author, Summary = a.Summary,
        ImageUrl = a.ImageUrl, CreatedAt = a.CreatedAt, PublishedAt = a.PublishedAt, ReadAt = a.ReadAt,
        LastReadAt = a.LastReadAt, DeletedAt = a.DeletedAt, ReadingStatus = a.ReadingStatus,
        ProgressPercent = a.ProgressPercent, IsFavorite = a.IsFavorite, IsArchived = a.IsArchived,
        ContentKind = a.ContentKind, SavedFromExtension = a.SavedFromExtensionAt != null,
        Tags = a.Tags.Select(t => new TagDto { Id = t.Id, Name = t.Name, Color = t.Color.Value, CreatedAt = t.CreatedAt }).ToList(),
        Feeds = a.FeedEntries.Select(e => new ArticleFeedDto(e.Feed.Id, e.Feed.Title)).Distinct().ToList(),
        HighlightCount = a.Highlights.Count()
    };

    internal static readonly Expression<Func<Article, ArticleListItemDto>> ListProjection = CreateListProjection();

    private static Expression<Func<Article, ArticleListItemDto>> CreateListProjection()
    {
        // SQL list queries deduplicate feeds after reading the page to avoid APPLY.
        var initializer = (MemberInitExpression)Projection.Body;
        var feeds = (MemberAssignment)initializer.Bindings.Single(binding => binding.Member.Name == nameof(Feeds));
        var toList = (MethodCallExpression)feeds.Expression;
        var distinct = (MethodCallExpression)toList.Arguments[0];
        var allFeeds = Expression.Bind(feeds.Member, toList.Update(toList.Object, distinct.Arguments));
        var bindings = initializer.Bindings.Select(binding => binding == feeds ? allFeeds : binding);

        return Projection.Update(initializer.Update(initializer.NewExpression, bindings), Projection.Parameters);
    }
}

public record ArticleFeedDto(Guid Id, string Title);
public record ArticleListPage(List<ArticleListItemDto> Items, int Total, string? NextCursor);
