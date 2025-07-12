using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Data;
using MiniCc.Api.Models;
using MiniCc.Api.Models.Dtos;
using MiniCc.Api.Extensions;

namespace MiniCc.Api.Services;

public class TagService : ITagService
{
    private readonly MiniCcContext _context;
    private readonly ILogger<TagService> _logger;

    public TagService(MiniCcContext context, ILogger<TagService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<TagWithArticleCountDto>> GetTagsAsync(string? search = null)
    {
        var query = _context.Tags.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(t => t.Name.Contains(search));
        }

        var tags = await query
            .OrderBy(t => t.Name)
            .Select(t => new TagWithArticleCountDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                CreatedAt = t.CreatedAt,
                ArticleCount = t.Articles.Count()
            })
            .ToListAsync();

        return tags;
    }

    public async Task<TagWithArticlesDto?> GetTagByIdAsync(Guid id)
    {
        var tag = await _context.Tags
            .Include(t => t.Articles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag == null)
        {
            return null;
        }

        return new TagWithArticlesDto
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            CreatedAt = tag.CreatedAt,
            Articles = tag.Articles.Select(a => new ArticleSummaryDto
            {
                Id = a.Id,
                Title = a.Title,
                Author = a.Author,
                Summary = a.Summary,
                CreatedAt = a.CreatedAt,
                ImageUrl = a.ImageUrl,
                IsFavorite = a.IsFavorite,
                IsArchived = a.IsArchived
            }).ToList()
        };
    }

    public async Task<IEnumerable<Article>> GetTagArticlesAsync(Guid tagId, int page = 1, int pageSize = 20)
    {
        return await _context.Articles
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .Where(a => a.Tags.Any(t => t.Id == tagId))
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task DeleteTagAsync(Guid id)
    {
        var tag = await _context.Tags
            .Include(t => t.Articles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tag != null)
        {
            // 从所有文章中移除该标签的关联
            tag.Articles.Clear();
            
            // 删除标签
            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetTagArticleCountAsync(Guid tagId)
    {
        return await _context.Articles
            .Where(a => a.Tags.Any(t => t.Id == tagId))
            .CountAsync();
    }
}