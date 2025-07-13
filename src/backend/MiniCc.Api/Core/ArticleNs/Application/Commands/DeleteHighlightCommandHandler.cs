using MediatR;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class DeleteHighlightCommandHandler : IRequestHandler<DeleteHighlightCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteHighlightCommandHandler> _logger;

    public DeleteHighlightCommandHandler(IUnitOfWork unitOfWork, ILogger<DeleteHighlightCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteHighlightCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var highlight = await _unitOfWork.Highlights.GetByIdAsync(request.Id, cancellationToken);
            if (highlight == null)
            {
                return Result<bool>.Failure($"Highlight with ID {request.Id} not found");
            }

            await _unitOfWork.Highlights.DeleteAsync(highlight, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting highlight {Id}", request.Id);
            return Result<bool>.Failure($"Failed to delete highlight: {ex.Message}");
        }
    }
}
