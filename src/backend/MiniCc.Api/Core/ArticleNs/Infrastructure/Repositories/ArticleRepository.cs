using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.ArticleNs.Infrastructure.Repositories;

public class ArticleRepository : IArticleRepository
{
    private readonly MiniCcDbContext _context;

    public ArticleRepository(MiniCcDbContext context)
    {
        _context = context;
    }

    public async Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Articles
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Article?> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return await _context.Articles
            .FirstOrDefaultAsync(a => a.Url.Value == url, cancellationToken);
    }

    public async Task<IEnumerable<Article>> GetAllAsync(int page, int pageSize, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Articles
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a => a.SearchVector.Matches(search));
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<Article> AddAsync(Article article, CancellationToken cancellationToken = default)
    {
        var entityEntry = await _context.Articles.AddAsync(article, cancellationToken);
        return entityEntry.Entity;
    }

    public Task UpdateAsync(Article article, CancellationToken cancellationToken = default)
    {
        _context.Articles.Update(article);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Article article, CancellationToken cancellationToken = default)
    {
        _context.Articles.Remove(article);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Articles.AnyAsync(a => a.Id == id, cancellationToken);
    }
}