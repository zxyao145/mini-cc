using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Shared.Data.Common;
using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

public class Tag : BaseAuditableEntity, IAggregateRoot
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public TagColor Color { get; set; } = TagColor.Default;

    public virtual ICollection<Article> Articles { get; set; } = new List<Article>();

    private Tag(Guid id, string name, TagColor color) : base(id)
    {
        Name = name;
        Color = color;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Tag() : base()
    {
    }

    public static Tag Create(string name, TagColor? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name cannot be null or empty", nameof(name));

        var id = UuidUtil.NewGuidV7();
        return new Tag(id, name.Trim(), color ?? TagColor.Default);
    }

    public void UpdateColor(TagColor color)
    {
        Color = color;
    }
}