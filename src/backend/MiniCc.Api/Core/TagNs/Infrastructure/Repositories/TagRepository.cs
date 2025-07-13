using Mapster;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.TagNs.Infrastructure.Repositories;

public class TagRepository : ITagRepository
{
    private readonly MiniCcDbContext _context;

    public TagRepository(MiniCcDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tags
            .Include(t=>t.Articles)
            .AsNoTracking()
            .FirstOrDefaultAsync(x=>x.Id == id, cancellationToken);
    }

    public async Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Tags
            .FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
    }

    public async Task<IEnumerable<Tag>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tags
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tag> AddAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        var entityEntry = await _context.Tags.AddAsync(tag, cancellationToken);
        return entityEntry.Entity;
    }

    public Task UpdateAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        _context.Tags.Update(tag);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Tag tag, CancellationToken cancellationToken = default)
    {
        _context.Tags.Remove(tag);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tags.AnyAsync(t => t.Id == id, cancellationToken);
    }
}
