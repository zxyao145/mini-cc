using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetHighlightsQuery : IRequest<Result<IEnumerable<HighlightDto>>>
{
}
