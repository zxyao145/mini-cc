using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ArticleNs.Domain.Services;

public interface IArticleDomainService
{
    Article CreateArticleFromContent(string url, string originalContent, string readableContent, string title, string author, string summary, string imageUrl);
}
