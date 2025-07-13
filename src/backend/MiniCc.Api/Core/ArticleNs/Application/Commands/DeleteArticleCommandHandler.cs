using MediatR;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class DeleteArticleCommandHandler : IRequestHandler<DeleteArticleCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<DeleteArticleCommandHandler> _logger;

    public DeleteArticleCommandHandler(
        IUnitOfWork unitOfWork,
        IArticleDomainService articleDomainService,
        ILogger<DeleteArticleCommandHandler> logger,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result> Handle(DeleteArticleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(request.ArticleId, cancellationToken);
            if (article == null)
            {
                return Result.Failure("article not found");
            }
            await _unitOfWork.Articles.DeleteAsync(article);

            return Result.Success();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving highlight from URL: {ArticleId}", request.ArticleId);
            return Result.Failure($"Failed to save article: {ex.Message}");
        }
    }
}
