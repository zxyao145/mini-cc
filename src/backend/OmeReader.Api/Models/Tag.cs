using System.ComponentModel.DataAnnotations;

namespace OmeReader.Api.Models;

public class Tag
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string Color { get; set; } = "#3B82F6";
    
    public DateTimeOffset CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Article> Articles { get; set; } = new List<Article>();
}