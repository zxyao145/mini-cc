using HtmlAgilityPack;

namespace ContentHandler;

public class ContentHandleResult
{
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string Summary { get; set; } = "";

    public string? Author { get; set; }
    public string? OriginalContent { get; set; }

    public string? Content { get; set; }
    public string? ContentType { get; set; }

    public HtmlDocument? Dom { get; set; }
}

public abstract class ContentHandlerBase
{
    public virtual int Order { get; } = 0;

    public virtual Task<string?> ResolveAsync(string url)
    {
        return Task.FromResult<string?>(url);
    }

    public virtual bool ShouldHandle(string url)
    {
        return false;
    }

    public virtual Task<ContentHandleResult> HandleAsync(string url)
    {
        return Task.FromResult(new ContentHandleResult { Url = url });
    }
}