using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetHighlightsQueryHandler : IRequestHandler<GetHighlightsQuery, Result<IEnumerable<HighlightDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetHighlightsQueryHandler> _logger;

    public GetHighlightsQueryHandler(IUnitOfWork unitOfWork, ILogger<GetHighlightsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<HighlightDto>>> Handle(GetHighlightsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var highlights = await _unitOfWork.Highlights.GetAllWithIncludesAsync(
                h => h.Article,
                orderBy: q => q.OrderByDescending(h => h.CreatedAt),
                cancellationToken: cancellationToken);

            return Result<IEnumerable<HighlightDto>>.Success(highlights.Adapt<IEnumerable<HighlightDto>>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting highlights");
            return Result<IEnumerable<HighlightDto>>.Failure($"Failed to get highlights: {ex.Message}");
        }
    }
}
