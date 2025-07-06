using ContentHandler;
using MiniCc.Api.Models;

namespace MiniCc.Api.Services;

public interface IArticleService
{
    Task<Article> SaveArticleAsync(string url);
    Task<Article> SaveContentAsync(ContentFetchResult result);

    Task<IEnumerable<Article>> GetArticlesAsync(int page = 1, int pageSize = 20, string? search = null);
    Task<Article?> GetArticleByIdAsync(Guid id);
    Task<Article> UpdateArticleAsync(Guid id, Article article);
    Task DeleteArticleAsync(Guid id);
    Task<Article> ToggleFavoriteAsync(Guid id);
    Task<Article> ToggleArchiveAsync(Guid id);
    Task<Highlight> AddHighlightAsync(Guid articleId, Highlight highlight);
    Task DeleteHighlightAsync(Guid highlightId);
    Task<Tag> AddTagToArticleAsync(Guid articleId, string tagName, string? color = null);
    Task RemoveTagFromArticleAsync(Guid articleId, Guid tagId);
}