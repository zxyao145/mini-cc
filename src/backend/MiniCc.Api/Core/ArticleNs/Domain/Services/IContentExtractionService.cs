using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Infrastructure.Service;

namespace MiniCc.Api.Core.ArticleNs.Domain.Services;

public interface IContentExtractionService
{
    Task<ReadabilityResult> ExtractContentAsync(string url, string originalContent);
}