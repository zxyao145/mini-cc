using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class UpdateArticleStateCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    : IRequestHandler<UpdateArticleStateCommand, Result<ArticleListItemDto>>
{
    public async Task<Result<ArticleListItemDto>> Handle(UpdateArticleStateCommand request, CancellationToken cancellationToken)
    {
        var article = await unitOfWork.Articles.GetByIdAsync(request.ArticleId, cancellationToken);
        if (article == null) return Result<ArticleListItemDto>.Failure("article not found");

        if (request.IsFavorite.HasValue) article.SetFavorite(request.IsFavorite.Value);
        if (request.IsArchived.HasValue) article.SetArchived(request.IsArchived.Value);
        if (request.ReadingStatus == "read") article.MarkRead(DateTimeOffset.UtcNow);
        if (request.ReadingStatus == "unread") article.MarkUnread();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ArticleListItemDto>.Success(mapper.Map<ArticleListItemDto>(article));
    }
}
