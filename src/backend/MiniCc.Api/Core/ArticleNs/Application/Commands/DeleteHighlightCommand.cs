using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class DeleteHighlightCommand : IRequest<Result<bool>>
{
    public Guid Id { get; }

    public DeleteHighlightCommand(Guid id)
    {
        Id = id;
    }
}
