using MediatR;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class RemoveTagFromArticleCommandHandler : IRequestHandler<RemoveTagFromArticleCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<RemoveTagFromArticleCommandHandler> _logger;

    public RemoveTagFromArticleCommandHandler(
        IUnitOfWork unitOfWork,
        IArticleDomainService articleDomainService,
        ILogger<RemoveTagFromArticleCommandHandler> logger,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result> Handle(RemoveTagFromArticleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(request.ArticleId, cancellationToken);
            if (article == null)
            {
                return Result.Failure("article not found");
            }
            var tag = await _unitOfWork.Tags.GetByIdAsync(request.TagId);
            if (tag != null)
            {
                article.Tags.Remove(tag);
                await _unitOfWork.SaveChangesAsync();
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error to remove tag from article: {ArticleId}", request.ArticleId);
            return Result.Failure($"Failed to remove tag article: {ex.Message}");
        }
    }
}
