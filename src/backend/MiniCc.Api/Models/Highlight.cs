using System.ComponentModel.DataAnnotations;

namespace OmeReader.Api.Models;

public class Highlight
{
    public int Id { get; set; }
    
    [Required]
    public string Text { get; set; } = string.Empty;
    
    public string Note { get; set; } = string.Empty;
    
    public string Color { get; set; } = "#FBBF24";
    
    public int StartOffset { get; set; }
    
    public int EndOffset { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int ArticleId { get; set; }
    
    public Article Article { get; set; } = null!;
}