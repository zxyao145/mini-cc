using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Common;
using System.Linq.Expressions;

namespace MiniCc.Api.Core.ArticleNs.Infrastructure.Repositories;

public class HighlightRepository : IHighlightRepository
{
    private readonly MiniCcDbContext _context;

    public HighlightRepository(MiniCcDbContext context)
    {
        _context = context;
    }

    public async Task<Highlight?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Highlights.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<IEnumerable<Highlight>> GetByArticleIdAsync(Guid articleId, CancellationToken cancellationToken = default)
    {
        return await _context.Highlights
            .Where(h => h.ArticleId == articleId)
            .OrderBy(h => h.StartOffset)
            .ToListAsync(cancellationToken);
    }

    public async Task<Highlight> AddAsync(Highlight highlight, CancellationToken cancellationToken = default)
    {
        var entityEntry = await _context.Highlights.AddAsync(highlight, cancellationToken);
        return entityEntry.Entity;
    }

    public Task UpdateAsync(Highlight highlight, CancellationToken cancellationToken = default)
    {
        _context.Highlights.Update(highlight);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Highlight highlight, CancellationToken cancellationToken = default)
    {
        _context.Highlights.Remove(highlight);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Highlights.AnyAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<Highlight?> GetByIdWithIncludesAsync<TProperty>(
        Guid id,
        Expression<Func<Highlight, TProperty>> navigationPropertyPath,
        CancellationToken cancellationToken)
    {

        return await _context.Highlights
            .Include(navigationPropertyPath)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    }

    public async Task<List<Highlight>> GetAllAsync<TOrederByKey>(
         Expression<Func<Highlight, bool>> predicate,
         Expression<Func<Highlight, TOrederByKey>> keySelector,
         CancellationToken cancellationToken)
    {
        return await _context.Highlights
            .AsNoTracking()
            .Where(predicate)
            .OrderBy(keySelector)
            .ToListAsync(cancellationToken);
    }


    public async Task<List<Highlight>> GetAllWithIncludesAsync<TProperty>(
        Expression<Func<Highlight, TProperty>> navigationPropertyPath,
        Func<IQueryable<Highlight>, IQueryable<Highlight>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Highlights
            .Include(navigationPropertyPath)
            .AsNoTracking();
        if (orderBy != null)
        {
            query = orderBy(query);
        }

        return await query.ToListAsync(cancellationToken);
    }
}