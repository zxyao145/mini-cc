using MiniCc.Api.Core.TagNs.Application.DTOs;

namespace MiniCc.Api.Core.ArticleNs.Application.DTOs;

public class ArticleLightDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsArchived { get; set; }
    public bool IsFavorite { get; set; }
    public List<TagDto> Tags { get; set; } = new();
}