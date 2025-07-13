using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public record GetArticleByIdQuery(Guid Id) : IRequest<Result<ArticleDto>>;
