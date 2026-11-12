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

    // Called inside the progress transaction: PostgreSQL holds this row lock until commit.
    public Task<Article?> GetByIdForProgressUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Articles.FromSqlInterpolated($"SELECT * FROM \"Articles\" WHERE \"Id\" = {id} AND \"DeletedAt\" IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Article?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Articles
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .Include(a => a.FeedEntries).ThenInclude(e => e.Feed)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Article?> GetByIdIncludingDeletedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Articles.IgnoreQueryFilters()
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .Include(a => a.FeedEntries).ThenInclude(e => e.Feed)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Article?> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        return await _context.Articles.FromSqlInterpolated($"SELECT * FROM \"Articles\" WHERE \"NormalizedUrl\" = {url} ORDER BY \"CreatedAt\", \"Id\" LIMIT 1 FOR UPDATE").IgnoreQueryFilters()
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .Include(a => a.FeedEntries).ThenInclude(e => e.Feed)
            .Where(a => a.NormalizedUrl == url)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
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