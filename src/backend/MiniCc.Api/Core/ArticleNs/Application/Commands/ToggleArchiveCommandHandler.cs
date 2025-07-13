using Mapster;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class ToggleArchiveCommandHandler : IRequestHandler<ToggleArchiveCommand, Result<ArticleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<ToggleArchiveCommandHandler> _logger;

    public ToggleArchiveCommandHandler(
        IUnitOfWork unitOfWork,
        IArticleDomainService articleDomainService,
        ILogger<ToggleArchiveCommandHandler> logger,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result<ArticleDto>> Handle(ToggleArchiveCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(request.ArticleId, cancellationToken);
            if (article == null)
            {
                return Result<ArticleDto>.Failure("article not found");
            }

            article.ToggleArchive();
            await _unitOfWork.SaveChangesAsync();
            return Result<ArticleDto>.Success(article.Adapt<ArticleDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving highlight from URL: {ArticleId}", request.ArticleId);
            return Result<ArticleDto>.Failure($"Failed to save article: {ex.Message}");
        }
    }
}
