using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.ArticleNs.Domain.Events;

public class ArticleCreatedEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public string Url { get; }
    public string Title { get; }

    public ArticleCreatedEvent(Guid articleId, string url, string title)
    {
        ArticleId = articleId;
        Url = url;
        Title = title;
    }
}

public class ArticleUpdatedEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public string Title { get; }

    public ArticleUpdatedEvent(Guid articleId, string title)
    {
        ArticleId = articleId;
        Title = title;
    }
}

public class ArticleReadEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public string Title { get; }

    public ArticleReadEvent(Guid articleId, string title)
    {
        ArticleId = articleId;
        Title = title;
    }
}

public class ArticleFavoritedEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public string Title { get; }
    public bool IsFavorite { get; }

    public ArticleFavoritedEvent(Guid articleId, string title, bool isFavorite)
    {
        ArticleId = articleId;
        Title = title;
        IsFavorite = isFavorite;
    }
}

public class ArticleArchivedEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public string Title { get; }
    public bool IsArchived { get; }

    public ArticleArchivedEvent(Guid articleId, string title, bool isArchived)
    {
        ArticleId = articleId;
        Title = title;
        IsArchived = isArchived;
    }
}