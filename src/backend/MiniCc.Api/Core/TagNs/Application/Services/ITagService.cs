using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Application.Commands;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.TagNs.Application.Services;

public interface ITagService
{
    Task<IEnumerable<TagWithArticleCountDto>> GetTagsAsync(string? search = null);
    Task<TagWithArticlesDto?> GetTagByIdAsync(Guid id);
    Task<IEnumerable<Article>> GetTagArticlesAsync(Guid tagId, int page = 1, int pageSize = 20);
    Task DeleteTagAsync(Guid id);
    Task<int> GetTagArticleCountAsync(Guid tagId);
    Task<Tag> CreateTagAsync(CreateTagCommand request);
}