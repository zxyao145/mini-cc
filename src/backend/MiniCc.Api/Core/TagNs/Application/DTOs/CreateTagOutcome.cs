namespace MiniCc.Api.Core.TagNs.Application.DTOs;

public abstract record CreateTagOutcome
{
    private CreateTagOutcome() { }

    public sealed record Created(TagWithArticleCountDto Tag) : CreateTagOutcome;
    public sealed record Conflict(Guid ExistingTagId) : CreateTagOutcome;
}
