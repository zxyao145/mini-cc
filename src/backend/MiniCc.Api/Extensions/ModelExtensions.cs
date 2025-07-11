using Mapster;
using MiniCc.Api.Models;
using MiniCc.Api.Models.Dtos;

namespace MiniCc.Api.Extensions;

public static class ModelExtensions
{
    /// <summary>
    /// 将 Article 实体转换为 ArticleDto
    /// </summary>
    public static ArticleDto ToDto(this Article article)
    {
        return article.Adapt<ArticleDto>();
    }

    /// <summary>
    /// 将 Tag 实体转换为 TagDto
    /// </summary>
    public static TagDto ToDto(this Tag tag)
    {
        return tag.Adapt<TagDto>();
    }

    /// <summary>
    /// 将 Highlight 实体转换为 HighlightDto
    /// </summary>
    public static HighlightDto ToDto(this Highlight highlight)
    {
        return highlight.Adapt<HighlightDto>();
    }

    /// <summary>
    /// 将 Article 集合转换为 ArticleDto 集合
    /// </summary>
    public static List<ArticleDto> ToDto(this IEnumerable<Article> articles)
    {
        return articles.Adapt<List<ArticleDto>>();
    }

    /// <summary>
    /// 将 Tag 集合转换为 TagDto 集合
    /// </summary>
    public static List<TagDto> ToDto(this IEnumerable<Tag> tags)
    {
        return tags.Adapt<List<TagDto>>();
    }

    /// <summary>
    /// 将 Highlight 集合转换为 HighlightDto 集合
    /// </summary>
    public static List<HighlightDto> ToDto(this IEnumerable<Highlight> highlights)
    {
        return highlights.Adapt<List<HighlightDto>>();
    }
}