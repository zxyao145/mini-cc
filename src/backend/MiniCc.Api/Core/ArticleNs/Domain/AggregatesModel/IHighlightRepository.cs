using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using System.Linq.Expressions;

namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

public interface IHighlightRepository
{
    Task<Highlight?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<Highlight>> GetByArticleIdAsync(Guid articleId, CancellationToken cancellationToken = default);

    Task<Highlight> AddAsync(Highlight highlight, CancellationToken cancellationToken = default);

    Task UpdateAsync(Highlight highlight, CancellationToken cancellationToken = default);

    Task DeleteAsync(Highlight highlight, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Highlight?> GetByIdWithIncludesAsync<TProperty>(
        Guid id,
        Expression<Func<Highlight, TProperty>> navigationPropertyPath,
        CancellationToken cancellationToken);

    Task<List<Highlight>> GetAllAsync<TOrederByKey>(
        Expression<Func<Highlight, bool>> predicate,
        Expression<Func<Highlight, TOrederByKey>> keySelector,
        CancellationToken cancellationToken);

    Task<List<Highlight>> GetAllWithIncludesAsync<TProperty>(
        Expression<Func<Highlight, TProperty>> navigationPropertyPath,
        Func<IQueryable<Highlight>, IQueryable<Highlight>>? orderBy = null,
        CancellationToken cancellationToken = default);
}