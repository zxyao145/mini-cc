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
}

public record ArticleFeedDto(Guid Id, string Title);
public record ArticleListPage(List<ArticleListItemDto> Items, int Total, string? NextCursor);
