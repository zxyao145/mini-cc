using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;

namespace MiniCc.Api.Core.ArticleNs.Domain.Services;

public interface IContentExtractionService
{
    Task<Content> ExtractContentAsync(string url, string originalContent);
}