using Mapster;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetHighlightByIdQueryHandler : IRequestHandler<GetHighlightByIdQuery, Result<HighlightDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetHighlightByIdQueryHandler> _logger;

    public GetHighlightByIdQueryHandler(IUnitOfWork unitOfWork, ILogger<GetHighlightByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<HighlightDto>> Handle(GetHighlightByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var highlight = await _unitOfWork.Highlights.GetByIdWithIncludesAsync(
                request.Id,
                h => h.Article,
                cancellationToken);

            if (highlight == null)
            {
                return Result<HighlightDto>.Failure($"Highlight with ID {request.Id} not found");
            }

            return Result<HighlightDto>.Success(highlight.Adapt<HighlightDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting highlight {Id}", request.Id);
            return Result<HighlightDto>.Failure($"Failed to get highlight: {ex.Message}");
        }
    }
}
