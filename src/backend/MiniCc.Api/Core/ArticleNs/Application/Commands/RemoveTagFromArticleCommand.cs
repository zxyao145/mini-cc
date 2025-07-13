using MediatR;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record RemoveTagFromArticleCommand(
    Guid ArticleId,
    Guid TagId
) : IRequest<Result>;
