using ContentHandler;
using MiniCc.Api.Models;

namespace MiniCc.Api.Services;

public interface IArticleService
{
    Task<Article> SaveArticleAsync(string url);
    Task<Article> SaveContentAsync(ContentFetchResult result);

    Task<IEnumerable<Article>> GetArticlesAsync(int page = 1, int pageSize = 20, string? search = null);
    Task<Article?> GetArticleByIdAsync(int id);
    Task<Article> UpdateArticleAsync(int id, Article article);
    Task DeleteArticleAsync(int id);
    Task<Article> ToggleFavoriteAsync(int id);
    Task<Article> ToggleArchiveAsync(int id);
    Task<Highlight> AddHighlightAsync(int articleId, Highlight highlight);
    Task DeleteHighlightAsync(int highlightId);
    Task<Tag> AddTagToArticleAsync(int articleId, string tagName, string? color = null);
    Task RemoveTagFromArticleAsync(int articleId, int tagId);
}