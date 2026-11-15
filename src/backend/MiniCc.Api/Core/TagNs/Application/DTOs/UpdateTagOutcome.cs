namespace MiniCc.Api.Core.TagNs.Application.DTOs;

public abstract record UpdateTagOutcome
{
    private UpdateTagOutcome() { }

    public sealed record Updated : UpdateTagOutcome;
    public sealed record NotFound : UpdateTagOutcome;
    public sealed record Conflict(Guid ExistingTagId) : UpdateTagOutcome;
}
