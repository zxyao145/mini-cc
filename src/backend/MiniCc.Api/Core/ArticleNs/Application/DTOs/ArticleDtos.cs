using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ArticleNs.Application.DTOs;

public class ArticleDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ReadableContent { get; set; } = string.Empty;
    public int TextContentLength { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsArchived { get; set; }
    public bool IsFavorite { get; set; }
    public ReadingStatus ReadingStatus { get; set; }
    public DateTimeOffset? LastReadAt { get; set; }
    public int? ProgressTextOffset { get; set; }
    public int? ProgressBlockIndex { get; set; }
    public double? ProgressBlockRatio { get; set; }
    public double? ProgressPercent { get; set; }
    public DateTimeOffset? ProgressUpdatedAt { get; set; }
    public string? ProgressSessionId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string NormalizedUrl { get; set; } = "";
    public ContentKind ContentKind { get; set; }
    public DateTimeOffset? SavedFromExtensionAt { get; set; }
    public ArticleProgressDto? Progress => ProgressBlockIndex.HasValue
        ? new(ProgressTextOffset, ProgressBlockIndex.Value, ProgressBlockRatio ?? 0, ProgressPercent ?? 0, ProgressUpdatedAt) : null;
    public bool SavedFromExtension => SavedFromExtensionAt.HasValue;
    public List<ArticleFeedDto> Feeds { get; set; } = new();
    public List<TagDto> Tags { get; set; } = new();
    public List<HighlightDto> Highlights { get; set; } = new();
}
public record ArticleProgressDto(int? TextOffset, int BlockIndex, double BlockRatio, double Percent, DateTimeOffset? UpdatedAt);
