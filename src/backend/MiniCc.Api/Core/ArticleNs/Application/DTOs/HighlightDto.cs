namespace MiniCc.Api.Core.ArticleNs.Application.DTOs;

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