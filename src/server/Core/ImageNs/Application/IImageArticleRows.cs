using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ImageNs.Application;

public interface IImageArticleRows
{
    Task<int> UpdateLockedAsync(Guid id, Func<Article, int> update, CancellationToken cancellationToken);
}
