using MiniCc.Api.Models;
using MiniCc.Api.Models.Dtos;

namespace MiniCc.Api.Services;

public interface IHighlightService
{
    /// <summary>
    /// 获取所有高亮列表
    /// </summary>
    Task<IEnumerable<Highlight>> GetHighlightsAsync();

    /// <summary>
    /// 根据ID获取高亮详情
    /// </summary>
    Task<Highlight?> GetHighlightByIdAsync(Guid id);

    /// <summary>
    /// 根据文章ID获取高亮列表
    /// </summary>
    Task<IEnumerable<Highlight>> GetHighlightsByArticleIdAsync(Guid articleId);

    /// <summary>
    /// 创建新高亮
    /// </summary>
    Task<Highlight> CreateHighlightAsync(Guid articleId, HighlightRequest highlightRequest);

    /// <summary>
    /// 更新高亮
    /// </summary>
    Task<Highlight?> UpdateHighlightAsync(Guid id, HighlightUpdateRequest updateRequest);

    /// <summary>
    /// 删除高亮
    /// </summary>
    Task<bool> DeleteHighlightAsync(Guid id);
}