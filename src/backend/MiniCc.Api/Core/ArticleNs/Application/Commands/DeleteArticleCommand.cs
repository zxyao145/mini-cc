using MediatR;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record DeleteArticleCommand(Guid ArticleId) : IRequest<Result>;
