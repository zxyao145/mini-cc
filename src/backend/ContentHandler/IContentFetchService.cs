namespace ContentHandler;

public interface IContentFetchService
{
    Task<ContentFetchResult?> FetchContentAsync(string url);
}