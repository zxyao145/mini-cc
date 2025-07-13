using Mapster;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Common;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class AddHighlightCommandHandler : IRequestHandler<AddHighlightCommand, Result<HighlightDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IArticleDomainService _articleDomainService;
    private readonly IMediator _mediator;
    private readonly ILogger<SaveArticleCommandHandler> _logger;

    public AddHighlightCommandHandler(
        IUnitOfWork unitOfWork,
        IArticleDomainService articleDomainService,
        ILogger<SaveArticleCommandHandler> logger,
        IMediator mediator)
    {
        _unitOfWork = unitOfWork;
        _articleDomainService = articleDomainService;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<Result<HighlightDto>> Handle(AddHighlightCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var articleResult = await _mediator.Send(new GetArticleByIdQuery(request.ArticleId));
            if (articleResult.IsFailure)
            {
                return Result<HighlightDto>.Failure(articleResult.Error);
            }

            var highlight = request.Adapt<Highlight>();
            highlight.Id = UuidUtil.NewGuidV7();
            highlight.CreatedAt = DateTimeOffset.UtcNow;

            // …Ë÷√ƒ¨»œ—’…´
            if (string.IsNullOrWhiteSpace(highlight.Color))
            {
                highlight.Color = "#FBBF24";
            }

            await _unitOfWork.Highlights.AddAsync(highlight);
            await _unitOfWork.SaveChangesAsync();

            return Result<HighlightDto>.Success(highlight.Adapt<HighlightDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving highlight from URL: {ArticleId}", request.ArticleId);
            return Result<HighlightDto>.Failure($"Failed to save article: {ex.Message}");
        }
    }
}
