using MiniCc.Api.Acl;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Infrastructure.Service;

public class ReadabilityResult
{
    public string? Content { get; set; }

    public string? TextContent { get; set; }

    public int Length { get; set; }

    public string? Excerpt { get; set; }

    public DateTimeOffset? PublishedDate { get; set; }

    public string? PreviewImage { get; set; }
    
    public string? Language { get; set; }

}

public class ContentExtractionService : IContentExtractionService
{
    private readonly IReadabilityApi _readabilityApi;

    public ContentExtractionService(IReadabilityApi readabilityApi)
    {
        _readabilityApi = readabilityApi;
    }

    public async Task<ReadabilityResult> ExtractContentAsync(string url, string originalContent)
    {
        var readabilityResult = await _readabilityApi.ParseAsync(url, originalContent);

        return readabilityResult;
    }
}