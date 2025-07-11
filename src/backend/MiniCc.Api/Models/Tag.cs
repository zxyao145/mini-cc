using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Models;

public class Tag
{
    public Guid Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Color { get; set; } = "#3B82F6";
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public virtual ICollection<Article> Articles { get; set; } = new List<Article>();
}