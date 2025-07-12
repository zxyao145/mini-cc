using MiniCc.Api.Models;
using MiniCc.Api.Models.Dtos;

namespace MiniCc.Api.Services;

public interface ITagService
{
    Task<IEnumerable<TagWithArticleCountDto>> GetTagsAsync(string? search = null);
    Task<TagWithArticlesDto?> GetTagByIdAsync(Guid id);
    Task<IEnumerable<Article>> GetTagArticlesAsync(Guid tagId, int page = 1, int pageSize = 20);
    Task DeleteTagAsync(Guid id);
    Task<int> GetTagArticleCountAsync(Guid tagId);
}