using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Events;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.Events;
using MiniCc.Api.Shared.Data.Common;
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

public class Article : AggregateRoot
{
    public Url Url { get; private set; }
    public string Title { get; private set; }
    public string Author { get; private set; }
    
    public string OriginalContent { get; private set; } = "";
    public string ReadableContent { get; private set; } = "";
    public int Length { get; private set; } 

    public string Summary { get; private set; }
    public string ImageUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public bool IsArchived { get; private set; }
    public bool IsFavorite { get; private set; }

    [JsonIgnore]
    public NpgsqlTsVector SearchVector { get; private set; } = default!;



    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public virtual ICollection<Highlight> Highlights { get; set; } = new List<Highlight>();

    private Article(Guid id, Url url, string title, string author, Content content,
                   string summary, string imageUrl) : base(id)
    {
        Url = url;
        Title = title;
        Author = author;
        ReadableContent = content.ReadableContent;
        OriginalContent = content.OriginalContent;
        Length = content.Length;
        Summary = summary;
        ImageUrl = imageUrl;
        CreatedAt = DateTimeOffset.UtcNow;
        IsArchived = false;
        IsFavorite = false;

        AddDomainEvent(new ArticleCreatedEvent(Id, Url.Value, Title));
    }

    public Article() : base()
    {
    }

    public static Article Create(Url url, string title, string author, Content content,
                                string summary = "", string imageUrl = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));

        var id = UuidUtil.NewGuidV7();
        return new Article(id, url, title, author ?? string.Empty, content,
                          summary ?? string.Empty, imageUrl ?? string.Empty);
    }

    public void UpdateMetadata(string title, string summary)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));

        Title = title;
        Summary = summary ?? string.Empty;

        AddDomainEvent(new ArticleUpdatedEvent(Id, Title));
    }

    public void MarkAsRead()
    {
        if (ReadAt == null)
        {
            ReadAt = DateTimeOffset.UtcNow;
            AddDomainEvent(new ArticleReadEvent(Id, Title));
        }
    }

    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        AddDomainEvent(new ArticleFavoritedEvent(Id, Title, IsFavorite));
    }

    public void ToggleArchive()
    {
        IsArchived = !IsArchived;
        AddDomainEvent(new ArticleArchivedEvent(Id, Title, IsArchived));
    }

    public void AddTag(Tag tag)
    {
        if (Tags.Any(t => t.Name == tag.Name))
            return;

        Tags.Add(tag);
        AddDomainEvent(new TagAddedToArticleEvent(Id, tag.Id, tag.Name));
    }

    public void RemoveTag(Guid tagId)
    {
        var tag = Tags.FirstOrDefault(t => t.Id == tagId);
        if (tag != null)
        {
            Tags.Remove(tag);
            AddDomainEvent(new TagRemovedFromArticleEvent(Id, tagId, tag.Name));
        }
    }

    public void AddHighlight(Highlight highlight)
    {
        Highlights.Add(highlight);
        AddDomainEvent(new HighlightAddedEvent(Id, highlight.Id, highlight.Text));
    }

    public void RemoveHighlight(Guid highlightId)
    {
        var highlight = Highlights.FirstOrDefault(h => h.Id == highlightId);
        if (highlight != null)
        {
            Highlights.Remove(highlight);
            AddDomainEvent(new HighlightRemovedEvent(Id, highlightId));
        }
    }
}