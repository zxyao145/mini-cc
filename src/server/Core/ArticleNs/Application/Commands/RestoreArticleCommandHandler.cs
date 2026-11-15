using MediatR;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class RestoreArticleCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    : IRequestHandler<RestoreArticleCommand, Result<ArticleListItemDto>>
{
    public async Task<Result<ArticleListItemDto>> Handle(RestoreArticleCommand request, CancellationToken cancellationToken)
    {
        var article = await unitOfWork.Articles.GetByIdIncludingDeletedAsync(request.ArticleId, cancellationToken);
        if (article == null) return Result<ArticleListItemDto>.Failure("article not found");

        article.Restore();
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The article was permanently deleted after it was loaded.
            return Result<ArticleListItemDto>.Failure("article not found");
        }

        return Result<ArticleListItemDto>.Success(mapper.Map<ArticleListItemDto>(article));
    }
}
