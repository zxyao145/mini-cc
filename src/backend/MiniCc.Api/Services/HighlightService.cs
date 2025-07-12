using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Data;
using MiniCc.Api.Models;
using MiniCc.Api.Models.Dtos;
using MiniCc.Api.Common;
using Mapster;

namespace MiniCc.Api.Services;

public class HighlightService : IHighlightService
{
    private readonly MiniCcContext _context;
    private readonly ILogger<HighlightService> _logger;

    public HighlightService(MiniCcContext context, ILogger<HighlightService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Highlight>> GetHighlightsAsync()
    {
        return await _context.Highlights
            .Include(h => h.Article)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
    }

    public async Task<Highlight?> GetHighlightByIdAsync(Guid id)
    {
        return await _context.Highlights
            .Include(h => h.Article)
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<IEnumerable<Highlight>> GetHighlightsByArticleIdAsync(Guid articleId)
    {
        return await _context.Highlights
            .Where(h => h.ArticleId == articleId)
            .OrderBy(h => h.StartOffset)
            .ToListAsync();
    }

    public async Task<Highlight> CreateHighlightAsync(Guid articleId, HighlightRequest highlightRequest)
    {
        // 验证文章是否存在
        var articleExists = await _context.Articles.AnyAsync(a => a.Id == articleId);
        if (!articleExists)
        {
            throw new ArgumentException($"Article with ID {articleId} does not exist.");
        }

        var highlight = highlightRequest.Adapt<Highlight>();
        highlight.Id = UuidUtil.NewGuidV7();
        highlight.ArticleId = articleId;
        highlight.CreatedAt = DateTimeOffset.UtcNow;

        // 设置默认颜色
        if (string.IsNullOrWhiteSpace(highlight.Color))
        {
            highlight.Color = "#FBBF24";
        }

        _context.Highlights.Add(highlight);
        await _context.SaveChangesAsync();

        return await GetHighlightByIdAsync(highlight.Id) ?? highlight;
    }

    public async Task<Highlight?> UpdateHighlightAsync(Guid id, HighlightUpdateRequest updateRequest)
    {
        var highlight = await _context.Highlights.FindAsync(id);
        if (highlight == null)
        {
            return null;
        }

        // 更新非空字段
        if (!string.IsNullOrWhiteSpace(updateRequest.Text))
        {
            highlight.Text = updateRequest.Text;
        }

        if (updateRequest.Note != null)
        {
            highlight.Note = updateRequest.Note;
        }

        if (!string.IsNullOrWhiteSpace(updateRequest.Color))
        {
            highlight.Color = updateRequest.Color;
        }

        if (updateRequest.StartOffset.HasValue)
        {
            highlight.StartOffset = updateRequest.StartOffset.Value;
        }

        if (updateRequest.EndOffset.HasValue)
        {
            highlight.EndOffset = updateRequest.EndOffset.Value;
        }

        await _context.SaveChangesAsync();
        return highlight;
    }

    public async Task<bool> DeleteHighlightAsync(Guid id)
    {
        var highlight = await _context.Highlights.FindAsync(id);
        if (highlight == null)
        {
            return false;
        }

        _context.Highlights.Remove(highlight);
        await _context.SaveChangesAsync();
        return true;
    }
}