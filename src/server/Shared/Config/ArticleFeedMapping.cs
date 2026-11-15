using Mapster;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Shared.Config;

// AddMapping scans IRegister implementations into the application's injected mapper configuration.
public class ArticleFeedMapping : IRegister
{
    public void Register(TypeAdapterConfig config) => config.ForType<Article, ArticleDto>()
        .Map(dto => dto.Feeds, article => article.FeedEntries
            .Select(entry => new ArticleFeedDto(entry.Feed.Id, entry.Feed.Title)).Distinct().ToList());
}
