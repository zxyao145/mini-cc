using Mapster;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class UpdateHighlightCommandHandler : IRequestHandler<UpdateHighlightCommand, Result<HighlightDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateHighlightCommandHandler> _logger;

    public UpdateHighlightCommandHandler(IUnitOfWork unitOfWork, ILogger<UpdateHighlightCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<HighlightDto>> Handle(UpdateHighlightCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var highlight = await _unitOfWork.Highlights.GetByIdAsync(request.Id, cancellationToken);
            if (highlight == null)
            {
                return Result<HighlightDto>.Failure($"Highlight with ID {request.Id} not found");
            }

            // Update non-null fields
            if (!string.IsNullOrWhiteSpace(request.Text))
            {
                highlight.Text = request.Text;
            }

            if (request.Note != null)
            {
                highlight.Note = request.Note;
            }

            if (!string.IsNullOrWhiteSpace(request.Color))
            {
                highlight.Color = request.Color;
            }

            if (request.StartOffset.HasValue)
            {
                highlight.StartOffset = request.StartOffset.Value;
            }

            if (request.EndOffset.HasValue)
            {
                highlight.EndOffset = request.EndOffset.Value;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<HighlightDto>.Success(highlight.Adapt<HighlightDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating highlight {Id}", request.Id);
            return Result<HighlightDto>.Failure($"Failed to update highlight: {ex.Message}");
        }
    }
}
