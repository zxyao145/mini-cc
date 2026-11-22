using Mapster;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Application;

namespace MiniCc.Api.Shared.Config;

public sealed class ArticleImageMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.ForType<Article, ArticleDto>()
            .Map(dto => dto.ReadableContent, article => MapContext.Current.GetService<ImageUrlResolver>()
                .ResolveHtml(article.ReadableContent, article.Images))
            .Map(dto => dto.ImageUrl, article => ResolveCover(article));

        config.ForType<Article, ArticleListItemDto>()
            .Map(dto => dto.ImageUrl, article => ResolveCover(article));

        config.ForType<Article, ArticleLightDto>()
            .Ignore(dto => dto.Url, dto => dto.ReadAt, dto => dto.Tags)
            .Map(dto => dto.ImageUrl, article => ResolveCover(article));
    }

    private static string ResolveCover(Article article) =>
        MapContext.Current.GetService<ImageUrlResolver>()
            .ResolveUrl(article.ImageUrl, article.Images, article.SavedFromExtensionAt.HasValue);
}
