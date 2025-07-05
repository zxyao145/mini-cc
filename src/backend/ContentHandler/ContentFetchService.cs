

using System.ComponentModel.DataAnnotations;

namespace ContentHandler;


public class ContentFetchResult
{
    [Required]
    public string Url { get; set; } = "";
    [Required]
    public string Title { get; set; } = "";
    [Required]
    public string OriginContent { get; set; } = "";

    public string Summary { get; set; } = "";

    public string Author { get; set; } = "";

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
                contentHandleResult = await HanbdlerLink(contentHandleResult);
                return new ContentFetchResult
                {
                    Url = contentHandleResult.Url ?? "",
                    Title = contentHandleResult.Title ?? "",
                    Author = contentHandleResult.Author ?? "",
                    OriginContent = contentHandleResult.OriginContent ?? "",
                    ContentType = contentHandleResult.ContentType ?? ""
                };
            }
        }

        return null;
    }

    private async Task<ContentHandleResult> HanbdlerLink(ContentHandleResult result)
    {
        // TODO: 更好的做法是，将这些数据下载到本地
        return result;
    }
}
