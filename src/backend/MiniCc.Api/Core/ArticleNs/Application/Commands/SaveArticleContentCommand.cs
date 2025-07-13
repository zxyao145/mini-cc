using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record SaveArticleContentCommand(
    string Url,
    string OriginalContent,
    string Title,
    string Author,
    string Summary,
    string ImageUrl
) : IRequest<Result<ArticleDto>>;
