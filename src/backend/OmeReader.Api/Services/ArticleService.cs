using ContentHandler;
using Microsoft.EntityFrameworkCore;
using OmeReader.Api.Data;
using OmeReader.Api.Models;

namespace OmeReader.Api.Services;

public class ArticleService : IArticleService
{
    private readonly IReadabilityApi _readabilityApi;
    private readonly OmeReaderContext _context;
    private readonly IContentFetchService _contentFetchService;
    private readonly ILogger<ArticleService> _logger;

    public ArticleService(OmeReaderContext context, IContentFetchService contentFetchService, ILogger<ArticleService> logger, IReadabilityApi readabilityApi)
    {
        _context = context;
        _contentFetchService = contentFetchService;
        _logger = logger;
        _readabilityApi = readabilityApi;
    }

    public async Task<Article> SaveArticleAsync(string url)
    {
        var existingArticle = await _context.Articles.FirstOrDefaultAsync(a => a.Url == url);
        if (existingArticle != null)
        {
            return existingArticle;
        }

        try
        {
            var result = await _contentFetchService.FetchContentAsync(url);
            if(result == null)
            {
                throw new InvalidOperationException($"Failed to fetch content from URL: {url}");
            }

            var readabilityContent = await _readabilityApi.ParseAsync(url, result.Content);

            var article = new Article
            {
                Url = url,

                Title = readabilityContent.Title ?? result.Title,
                Author = result.Author,
                OriginContent = result.OriginContent,
                ReadableContent = readabilityContent.Content ?? "",
                TextContentLegth = readabilityContent.Length,
                Summary = readabilityContent.Excerpt ?? result.Summary,
                CreatedAt = DateTime.UtcNow
            };

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return article;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save article from URL: {Url}", url);
            throw;
        }
    }

    public async Task<IEnumerable<Article>> GetArticlesAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var query = _context.Articles
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(a => a.Title.Contains(search) || 
                                   a.ReadableContent.Contains(search) || 
                                   a.Author.Contains(search));
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Article?> GetArticleByIdAsync(int id)
    {
        return await _context.Articles
            .Include(a => a.Tags)
            .Include(a => a.Highlights)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Article> UpdateArticleAsync(int id, Article article)
    {
        var existingArticle = await _context.Articles.FindAsync(id);
        if (existingArticle == null)
        {
            throw new ArgumentException($"Article with ID {id} not found");
        }

        existingArticle.Title = article.Title;
        existingArticle.Summary = article.Summary;
        existingArticle.IsFavorite = article.IsFavorite;
        existingArticle.IsArchived = article.IsArchived;
        existingArticle.ReadAt = article.ReadAt;

        await _context.SaveChangesAsync();
        return existingArticle;
    }

    public async Task DeleteArticleAsync(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article != null)
        {
            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Article> ToggleFavoriteAsync(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null)
        {
            throw new ArgumentException($"Article with ID {id} not found");
        }

        article.IsFavorite = !article.IsFavorite;
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task<Article> ToggleArchiveAsync(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null)
        {
            throw new ArgumentException($"Article with ID {id} not found");
        }

        article.IsArchived = !article.IsArchived;
        await _context.SaveChangesAsync();
        return article;
    }

    public async Task<Highlight> AddHighlightAsync(int articleId, Highlight highlight)
    {
        highlight.ArticleId = articleId;
        highlight.CreatedAt = DateTime.UtcNow;

        _context.Highlights.Add(highlight);
        await _context.SaveChangesAsync();
        return highlight;
    }

    public async Task DeleteHighlightAsync(int highlightId)
    {
        var highlight = await _context.Highlights.FindAsync(highlightId);
        if (highlight != null)
        {
            _context.Highlights.Remove(highlight);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Tag> AddTagToArticleAsync(int articleId, string tagName, string? color = null)
    {
        var article = await _context.Articles
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == articleId);
        
        if (article == null)
        {
            throw new ArgumentException($"Article with ID {articleId} not found");
        }

        var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
        if (existingTag == null)
        {
            existingTag = new Tag
            {
                Name = tagName,
                Color = color ?? "#3B82F6",
                CreatedAt = DateTime.UtcNow
            };
            _context.Tags.Add(existingTag);
        }

        if (!article.Tags.Any(t => t.Name == tagName))
        {
            article.Tags.Add(existingTag);
        }

        await _context.SaveChangesAsync();
        return existingTag;
    }

    public async Task RemoveTagFromArticleAsync(int articleId, int tagId)
    {
        var article = await _context.Articles
            .Include(a => a.Tags)
            .FirstOrDefaultAsync(a => a.Id == articleId);
        
        if (article != null)
        {
            var tag = article.Tags.FirstOrDefault(t => t.Id == tagId);
            if (tag != null)
            {
                article.Tags.Remove(tag);
                await _context.SaveChangesAsync();
            }
        }
    }
}