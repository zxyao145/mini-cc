using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.ArticleNs.Domain.Events;

public class HighlightAddedEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public Guid HighlightId { get; }
    public string SelectedText { get; }

    public HighlightAddedEvent(Guid articleId, Guid highlightId, string selectedText)
    {
        ArticleId = articleId;
        HighlightId = highlightId;
        SelectedText = selectedText;
    }
}

public class HighlightRemovedEvent : BaseEvent
{
    public Guid ArticleId { get; }
    public Guid HighlightId { get; }

    public HighlightRemovedEvent(Guid articleId, Guid highlightId)
    {
        ArticleId = articleId;
        HighlightId = highlightId;
    }
}