using Mapster;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetHighlightsByArticleIdQueryHandler : IRequestHandler<GetHighlightsByArticleIdQuery, Result<IEnumerable<HighlightDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetHighlightsByArticleIdQueryHandler> _logger;

    public GetHighlightsByArticleIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetHighlightsByArticleIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<HighlightDto>>> Handle(GetHighlightsByArticleIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var highlights = await _unitOfWork.Highlights.GetAllAsync(
                h => h.ArticleId == request.ArticleId,
                h => h.StartOffset,
                cancellationToken: cancellationToken);

            return Result<IEnumerable<HighlightDto>>.Success(highlights.Adapt<IEnumerable<HighlightDto>>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting highlights for article {ArticleId}", request.ArticleId);
            return Result<IEnumerable<HighlightDto>>.Failure($"Failed to get highlights: {ex.Message}");
        }
    }
}
