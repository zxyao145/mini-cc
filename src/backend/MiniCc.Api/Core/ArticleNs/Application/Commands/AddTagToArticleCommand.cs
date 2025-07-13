using MediatR;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record AddTagToArticleCommand(
    Guid ArticleId,
    string Name,
    string? Color
) : IRequest<Result<TagDto>>;
