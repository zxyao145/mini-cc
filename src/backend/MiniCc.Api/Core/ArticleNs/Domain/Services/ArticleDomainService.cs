using ContentHandler;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Infrastructure.Service;

namespace MiniCc.Api.Core.ArticleNs.Domain.Services;

public class ArticleDomainService : IArticleDomainService
{
    private readonly IContentFetchService _contentFetchService;
    private readonly IContentExtractionService _contentExtractionService;

    public ArticleDomainService(
        IContentFetchService contentFetchService,
        IContentExtractionService contentExtractionService)
    {
        _contentFetchService = contentFetchService;
        _contentExtractionService = contentExtractionService;
    }

    public async Task<Article> CreateArticleFromUrlAsync(string url)
    {
        var urlValue = Url.Create(url);

        var fetchResult = await _contentFetchService.FetchContentAsync(url);
        if (fetchResult == null)
        {
            throw new InvalidOperationException($"Failed to fetch content from URL: {url}");
        }

        var readabilityResult = await _contentExtractionService.ExtractContentAsync(url, fetchResult.OriginalContent);

        var content = Content.Create(
           fetchResult.OriginalContent,
           readabilityResult.Content ?? string.Empty,
           readabilityResult.Length
        );

        return Article.Create(
            urlValue,
            fetchResult.Title ?? "Untitled",
            fetchResult.Author ?? string.Empty,
            content,
            fetchResult.Summary ?? string.Empty,
            string.Empty
        );
    }

    public async Task<Article> CreateArticleFromContentAsync(
        string url,
        string originalContent,
        string title,
        string author,
        string summary,
        string imageUrl)
    {
        var urlValue = Url.Create(url);
        var readabilityResult = await _contentExtractionService.ExtractContentAsync(url, originalContent);

        var content = Content.Create(
           originalContent,
           readabilityResult.Content ?? string.Empty,
           readabilityResult.Length
        );

        return Article.Create(
            urlValue,
            title,
            author,
            content,
            summary,
            imageUrl
        );
    }
}