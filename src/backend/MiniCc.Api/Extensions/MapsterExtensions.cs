using Mapster;
using MiniCc.Api.Models;
using MiniCc.Api.Models.Dtos;

namespace MiniCc.Api.Extensions;

/// <summary>
/// Mapster 映射扩展方法
/// </summary>
public static class MapsterExtensions
{
    /// <summary>
    /// 将 ArticleDto 转换回 Article 实体（用于更新操作）
    /// </summary>
    public static Article ToEntity(this ArticleDto dto)
    {
        return dto.Adapt<Article>();
    }

    /// <summary>
    /// 将 TagDto 转换回 Tag 实体
    /// </summary>
    public static Tag ToEntity(this TagDto dto)
    {
        return dto.Adapt<Tag>();
    }

    /// <summary>
    /// 将 HighlightDto 转换回 Highlight 实体
    /// </summary>
    public static Highlight ToEntity(this HighlightDto dto)
    {
        return dto.Adapt<Highlight>();
    }

    /// <summary>
    /// 投影映射 - 只映射指定字段（用于性能优化）
    /// 创建一个不包含大字段的轻量级 ArticleDto
    /// </summary>
    public static ArticleDto ToLightDto(this Article article)
    {
        var dto = article.Adapt<ArticleDto>();
        dto.ReadableContent = string.Empty; // 清空大字段
        dto.Highlights = new List<HighlightDto>(); // 清空关联数据
        return dto;
    }

    /// <summary>
    /// 批量轻量映射
    /// </summary>
    public static List<ArticleDto> ToLightDto(this IEnumerable<Article> articles)
    {
        return articles.Select(a => a.ToLightDto()).ToList();
    }

    /// <summary>
    /// 条件映射 - 根据条件决定是否包含某些字段
    /// </summary>
    public static ArticleDto ToDtoConditional(this Article article, bool includeContent = true)
    {
        if (!includeContent)
        {
            return article.ToLightDto();
        }
        return article.ToDto();
    }
}