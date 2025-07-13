using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetArticleByIdQueryHandler : IRequestHandler<GetArticleByIdQuery, Result<ArticleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetArticleByIdQueryHandler> _logger;

    public GetArticleByIdQueryHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetArticleByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<ArticleDto>> Handle(GetArticleByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var article = await _unitOfWork.Articles.GetByIdAsync(request.Id, cancellationToken);
            if (article == null)
            {
                return Result<ArticleDto>.Failure("Article not found");
            }

            return Result<ArticleDto>.Success(_mapper.Map<ArticleDto>(article));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting article {Id}", request.Id);
            return Result<ArticleDto>.Failure($"Failed to get article: {ex.Message}");
        }
    }
}
