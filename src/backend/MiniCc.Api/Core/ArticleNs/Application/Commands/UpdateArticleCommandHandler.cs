using Mapster;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class UpdateArticleCommandHandler : IRequestHandler<UpdateArticleCommand, Result<ArticleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateArticleCommandHandler> _logger;

    public UpdateArticleCommandHandler(
        IUnitOfWork unitOfWork,
        IArticleDomainService articleDomainService,
        ILogger<UpdateArticleCommandHandler> logger,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result<ArticleDto>> Handle(UpdateArticleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(request.Id, cancellationToken);
            if (article == null)
            {
                return Result<ArticleDto>.Failure("article not found");
            }

            article.UpdateMetadata(request.Title, request.Summary);
            if (request.IsFavorite != null && article.IsFavorite != request.IsFavorite)
            {
                article.ToggleFavorite();
            }
            if (request.IsArchived != null && article.IsArchived != request.IsArchived)
            {
                article.ToggleArchive();
            }
            await _unitOfWork.SaveChangesAsync();
            return Result<ArticleDto>.Success(article.Adapt<ArticleDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error UpdateArticleCommand: {ArticleId}", request.Id);
            return Result<ArticleDto>.Failure($"Failed to update article: {ex.Message}");
        }
    }
}
