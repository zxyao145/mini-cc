using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Models;

public class Highlight
{
    public Guid Id { get; set; }
    
    [Required]
    public string Text { get; set; } = string.Empty;
    
    public string Note { get; set; } = string.Empty;
    
    public string Color { get; set; } = "#FBBF24";
    
    public int StartOffset { get; set; }
    
    public int EndOffset { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public Guid ArticleId { get; set; }
    
    public Article Article { get; set; } = null!;
}