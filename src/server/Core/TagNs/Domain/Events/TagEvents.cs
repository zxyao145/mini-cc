using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.TagNs.Domain.Events;

public class TagAddedToArticleEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public Guid TagId { get; }
    public string TagName { get; }

    public TagAddedToArticleEvent(Guid articleId, Guid tagId, string tagName)
    {
        ArticleId = articleId;
        TagId = tagId;
        TagName = tagName;
    }
}

public class TagRemovedFromArticleEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public Guid TagId { get; }
    public string TagName { get; }

    public TagRemovedFromArticleEvent(Guid articleId, Guid tagId, string tagName)
    {
        ArticleId = articleId;
        TagId = tagId;
        TagName = tagName;
    }
}