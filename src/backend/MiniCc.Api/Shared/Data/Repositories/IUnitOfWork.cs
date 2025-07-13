using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;

namespace MiniCc.Api.Shared.Data.Repositories;

public interface IUnitOfWork
{
    IArticleRepository Articles { get; }
    ITagRepository Tags { get; }
    IHighlightRepository Highlights { get; }
    IUserRepository Users { get; }
    IApiKeyRepository ApiKeys { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}