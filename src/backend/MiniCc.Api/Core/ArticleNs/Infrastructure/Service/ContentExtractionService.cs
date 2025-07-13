using MiniCc.Api.Acl;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Services;

namespace MiniCc.Api.Core.ArticleNs.Infrastructure.Service;

public class ReadabilityResult
{
    public string? Content { get; set; }
}

public class ContentExtractionService : IContentExtractionService
{
    private readonly IReadabilityApi _readabilityApi;

    public ContentExtractionService(IReadabilityApi readabilityApi)
    {
        _readabilityApi = readabilityApi;
    }

    public async Task<Content> ExtractContentAsync(string url, string originalContent)
    {
        var readabilityResult = await _readabilityApi.ParseAsync(url, originalContent);

        return Content.Create(
            originalContent,
            readabilityResult.Content ?? string.Empty
        );
    }
}