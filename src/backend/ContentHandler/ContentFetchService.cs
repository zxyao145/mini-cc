

namespace ContentHandler;


public class ContentFetchResult
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";

    public string Author { get; set; } = "";
    public string OriginContent { get; set; } = "";

    

    public string Content { get; set; } = "";
    public string ContentType { get; set; } = "";
}

public class ContentFetchService : IContentFetchService
{
    private readonly List<ContentHandlerBase> _contentHandlers;

    public ContentFetchService(IEnumerable<ContentHandlerBase> contentHandlerBases)
    {
        _contentHandlers = contentHandlerBases.OrderBy(x => x.Order).ToList();
    }

    public async Task<ContentFetchResult?> FetchContentAsync(string url)
    {

        foreach (var handler in _contentHandlers)
        {
            if (handler.ShouldHandle(url))
            {
                url = (await handler.ResolveAsync(url)) ?? "";
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var contentHandleResult = await handler.HandleAsync(url);
                return new ContentFetchResult
                {
                    Url = contentHandleResult.Url ?? "",
                    Title = contentHandleResult.Title ?? "",
                    Author = contentHandleResult.Author ?? "",
                    OriginContent = contentHandleResult.OriginContent ?? "",
                    Content = contentHandleResult.Content ?? "",
                    ContentType = contentHandleResult.ContentType ?? ""
                };
            }
        }

        return null;
    }
}
