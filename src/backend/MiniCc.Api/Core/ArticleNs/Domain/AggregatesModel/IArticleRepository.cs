namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

public interface IArticleRepository
{
    Task<Article?> GetByIdForProgressUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Article?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Article?> GetByUrlAsync(string url, CancellationToken cancellationToken = default);


    Task<Article> AddAsync(Article article, CancellationToken cancellationToken = default);

    Task UpdateAsync(Article article, CancellationToken cancellationToken = default);

    Task DeleteAsync(Article article, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}