using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record RestoreArticleCommand(Guid ArticleId) : IRequest<Result<ArticleListItemDto>>;
