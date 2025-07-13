using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetHighlightsByArticleIdQuery : IRequest<Result<IEnumerable<HighlightDto>>>
{
    public Guid ArticleId { get; }

    public GetHighlightsByArticleIdQuery(Guid articleId)
    {
        ArticleId = articleId;
    }
}
