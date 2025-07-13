using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ArticleNs.Domain.Services;

public interface IArticleDomainService
{
    Task<Article> CreateArticleFromUrlAsync(string url);

    Task<Article> CreateArticleFromContentAsync(string url, string originalContent, string title, string author, string summary, string imageUrl);
}