using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Events;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.Events;
using MiniCc.Api.Shared.Data.Common;
using NpgsqlTypes;
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
    public ReadingStatus ReadingStatus { get; private set; }
    public DateTimeOffset? LastReadAt { get; private set; }
    public int? ProgressTextOffset { get; private set; }
    public int? ProgressBlockIndex { get; private set; }
    public double? ProgressBlockRatio { get; private set; }
    public double? ProgressPercent { get; private set; }
    public DateTimeOffset? ProgressUpdatedAt { get; private set; }
    public string? ProgressSessionId { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string NormalizedUrl { get; private set; } = "";
    public ContentKind ContentKind { get; private set; }
    public DateTimeOffset? SavedFromExtensionAt { get; private set; }

    [JsonIgnore]
    public NpgsqlTsVector SearchVector { get; private set; } = default!;

    public virtual ICollection<MiniCc.Api.Core.FeedNs.Domain.FeedEntry> FeedEntries { get; set; } = new List<MiniCc.Api.Core.FeedNs.Domain.FeedEntry>();
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public virtual ICollection<Highlight> Highlights { get; set; } = new List<Highlight>();

    private Article(Guid id, Url url, string title, string author, string summary, string imageUrl) : base(id)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));

        Url = url;
        NormalizedUrl = UrlNormalizer.Normalize(url.Value);
        Title = title;
        Author = author ?? string.Empty;
        Summary = summary ?? string.Empty;
        ImageUrl = imageUrl ?? string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
        IsArchived = false;
        IsFavorite = false;

        AddDomainEvent(new ArticleCreatedEvent(Id, Url.Value, Title));
    }

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    public Article() : base()
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
    {
    }

    public static Article Create(Url url, string title, string author, Content content,
                                string summary = "", string imageUrl = "")
    {
        var article = new Article(UuidUtil.NewGuidV7(), url, title, author, summary, imageUrl);
        article.ReplaceContent(content, ContentKind.Full);
        return article;
    }

    public static Article CreateLink(Url url, string title, string author, string summary, string imageUrl) =>
        new Article(UuidUtil.NewGuidV7(), url, title, author, summary, imageUrl) { ContentKind = ContentKind.Link };

    public void UpdateMetadata(string title, string summary)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));

        Title = title;
        Summary = summary ?? string.Empty;

        AddDomainEvent(new ArticleUpdatedEvent(Id, Title));
    }

    public void FillMissingMetadata(string author, string summary, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(Author)) Author = author;
        if (string.IsNullOrWhiteSpace(Summary)) Summary = summary;
        if (string.IsNullOrWhiteSpace(ImageUrl)) ImageUrl = imageUrl;
    }

    public void SetFavorite(bool favorite)
    {
        if (IsFavorite == favorite) return;
        IsFavorite = favorite;
        AddDomainEvent(new ArticleFavoritedEvent(Id, Title, IsFavorite));
    }

    public void SetArchived(bool archived)
    {
        if (IsArchived == archived) return;
        IsArchived = archived;
        AddDomainEvent(new ArticleArchivedEvent(Id, Title, IsArchived));
    }

    public void MarkRead(DateTimeOffset now)
    {
        ReadingStatus = ReadingStatus.Read;
        if (ReadAt != null) return;
        ReadAt = now;
        AddDomainEvent(new ArticleReadEvent(Id, Title));
    }

    public void MarkUnread()
    {
        ReadingStatus = ReadingStatus.Unread;
        ReadAt = null;
    }

    public void ApplyProgress(ReadingPosition position, string sessionId, DateTimeOffset now)
    {
        ProgressTextOffset = position.TextOffset;
        ProgressBlockIndex = position.BlockIndex;
        ProgressBlockRatio = position.BlockRatio;
        ProgressPercent = position.Percent;
        ProgressSessionId = sessionId;
        ProgressUpdatedAt = now;
        LastReadAt = now;
        if (ReadingStatus == ReadingStatus.Unread) ReadingStatus = ReadingStatus.Reading;
    }

    public void ResetProgress(string? sessionId, DateTimeOffset now)
    {
        ProgressTextOffset = null;
        ProgressBlockIndex = null;
        ProgressBlockRatio = null;
        ProgressPercent = null;
        ProgressSessionId = sessionId;
        ProgressUpdatedAt = now;
    }

    public void MoveToTrash(DateTimeOffset now) => DeletedAt ??= now;

    public void Restore() => DeletedAt = null;

    public void ReplaceContent(Content content, ContentKind kind)
    {
        OriginalContent = content.OriginalContent;
        ReadableContent = content.ReadableContent;
        Length = content.Length;
        ContentKind = kind;
    }

    public void SetFeedContent(ContentKind kind, DateTimeOffset? publishedAt)
    {
        ContentKind = kind;
        PublishedAt = publishedAt;
    }

    public void BackfillNormalizedUrl()
    {
        if (NormalizedUrl.Length == 0) NormalizedUrl = UrlNormalizer.Normalize(Url.Value);
    }

    public void MarkSavedFromExtension(DateTimeOffset now) => SavedFromExtensionAt ??= now;

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