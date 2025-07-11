namespace MiniCc.Api.Models.Dtos;

public class ArticleDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string ReadableContent { get; set; } = string.Empty;
    public int TextContentLength { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsArchived { get; set; }
    public bool IsFavorite { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    
    public List<TagDto> Tags { get; set; } = new();
    public List<HighlightDto> Highlights { get; set; } = new();
}

public class TagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class HighlightDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid ArticleId { get; set; }
}