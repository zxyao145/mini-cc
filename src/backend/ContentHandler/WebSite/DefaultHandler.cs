using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace ContentHandler.WebSite;

public class DefaultHandler : ContentHandlerBase
{
    public override int Order => int.MaxValue;

    private readonly HttpClient _httpClient;
    private readonly ILogger<DefaultHandler> _logger;

    public DefaultHandler(ILogger<DefaultHandler> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("default");
    }

    public override bool ShouldHandle(string url)
    {
        return true;
    }

    public override Task<string?> ResolveAsync(string url)
    {
        return Task.FromResult<string?>(url);
    }

    public override async Task<ContentHandleResult> HandleAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var htmlContent = await response.Content.ReadAsStringAsync();

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            var title = ExtractTitle(htmlDoc);
            var summary = ExtractSummary(htmlContent);
            var author = ExtractAuthor(htmlDoc);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

            return new ContentHandleResult
            {
                Url = url,
                Title = title,
                Summary = summary,
                Author = author,
                OriginalContent = htmlContent,
                Content = htmlContent,
                ContentType = contentType,
                Dom = htmlDoc,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape URL: {Url}", url);
            throw;
        }
    }

    private string ExtractTitle(HtmlDocument htmlDoc)
    {
        var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
        var title = titleNode?.InnerText ?? string.Empty;
        return title;
    }

    private string ExtractSummary(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var words = content.Split(' ');
        if (words.Length <= 50)
            return content;

        return string.Join(" ", words.Take(50)) + "...";
    }

    private string ExtractAuthor(HtmlDocument doc)
    {
        var authorSelectors = new[]
        {
            "//meta[@name='author']",
            "//meta[@property='article:author']",
            "//meta[@name='dc.creator']",
        };

        foreach (var selector in authorSelectors)
        {
            var authorNodes = doc.DocumentNode.SelectNodes(selector);
            var authorNode = authorNodes?.FirstOrDefault();
            if (authorNode != null)
            {
                var author = authorNode.GetAttributeValue("content", "") ?? authorNode.InnerText?.Trim();
                if (!string.IsNullOrEmpty(author))
                    return author;
            }
        }

        return string.Empty;
    }
}