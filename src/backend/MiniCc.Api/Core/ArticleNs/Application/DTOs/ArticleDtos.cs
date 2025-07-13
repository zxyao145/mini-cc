using MiniCc.Api.Core.TagNs.Application.DTOs;

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
    public List<TagDto> Tags { get; set; } = new();
    public List<HighlightDto> Highlights { get; set; } = new();
}