using Mapster;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;

namespace MiniCc.Api.Shared.Config;

public static class MappingProfile
{
    public static void ConfigureMapster()
    {
        TypeAdapterConfig<Article, ArticleDto>
            .NewConfig()
            .Map(dest => dest.Url, src => src.Url.Value)
            .Map(dest => dest.ReadableContent, src => src.ReadableContent)
            .Map(dest => dest.TextContentLength, src => src.Length)
            .Map(dest => dest.Url, src => src.Url.Value)
            .IgnoreNullValues(true);  // 忽略 null 值


        // ArticleDto -> Article 反向映射（用于更新操作）
        TypeAdapterConfig<ArticleDto, Article>
            .NewConfig()
            .Ignore(dest => dest.Tags)        // 忽略导航属性
            .Ignore(dest => dest.Highlights)  // 忽略导航属性
            .Ignore(dest => dest.SearchVector); // 忽略计算字段


        TypeAdapterConfig<Article, ArticleLightDto>
            .NewConfig()
            .Map(dest => dest.Url, src => src.Url.Value);


        // Tag -> TagDto 映射配置
        TypeAdapterConfig<Tag, TagDto>
            .NewConfig()
            .Map(dest => dest.Color, src => src.Color.Value)
            .IgnoreNullValues(true);


        // TagDto -> Tag 反向映射
        TypeAdapterConfig<TagDto, Tag>
            .NewConfig()
            .Ignore(dest => dest.Articles); // 忽略导航属性

        // Highlight -> HighlightDto 映射配置
        TypeAdapterConfig<Highlight, HighlightDto>
            .NewConfig()
            .IgnoreNullValues(true);

        // HighlightDto -> Highlight 反向映射
        TypeAdapterConfig<HighlightDto, Highlight>
            .NewConfig()
            .Ignore(dest => dest.Article); // 忽略导航属性

        // 全局配置
        TypeAdapterConfig.GlobalSettings.Default
            .PreserveReference(true)  // 处理循环引用
            .MaxDepth(3)              // 设置最大深度防止无限递归
            .IgnoreNullValues(true)   // 全局忽略 null 值
            .RequireDestinationMemberSource(false); // 允许目标成员没有对应的源成员
    }

    public static void RegisterMappings()
    {
        // 确保映射配置被编译和缓存，提高性能
        TypeAdapterConfig.GlobalSettings.Compile();
    }
}