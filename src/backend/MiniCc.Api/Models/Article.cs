using NpgsqlTypes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Models;

public class Article
{
    public Guid Id { get; set; }

    [Required]
    public string Url { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string OriginContent { get; set; } = string.Empty;

    public string ReadableContent { get; set; } = string.Empty;

    public int TextContentLength { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? ReadAt { get; set; }
    
    public bool IsArchived { get; set; } = false;
    
    public bool IsFavorite { get; set; } = false;
    
    public string ImageUrl { get; set; } = string.Empty;


    // Ԥ�������������
    [JsonIgnore]
    public NpgsqlTsVector SearchVector { get; set; } = default!;

    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    
    public ICollection<Highlight> Highlights { get; set; } = new List<Highlight>();
}