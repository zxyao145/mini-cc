using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetHighlightByIdQuery : IRequest<Result<HighlightDto>>
{
    public Guid Id { get; }

    public GetHighlightByIdQuery(Guid id)
    {
        Id = id;
    }
}
