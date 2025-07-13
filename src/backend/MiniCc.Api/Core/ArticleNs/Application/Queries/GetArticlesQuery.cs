using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public record GetArticlesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IRequest<Result<IEnumerable<ArticleDto>>>;
