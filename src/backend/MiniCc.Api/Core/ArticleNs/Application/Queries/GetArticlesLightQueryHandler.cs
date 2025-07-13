
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetArticlesLightQueryHandler : IRequestHandler<GetArticlesLightQuery, Result<IEnumerable<ArticleLightDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetArticlesLightQueryHandler> _logger;

    public GetArticlesLightQueryHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetArticlesLightQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<ArticleLightDto>>> Handle(GetArticlesLightQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var articles = await _unitOfWork.Articles.GetAllAsync(
                request.Page, request.PageSize, request.Search, cancellationToken);

            var articleDtos = _mapper.Map<IEnumerable<ArticleLightDto>>(articles);

            return Result<IEnumerable<ArticleLightDto>>.Success(articleDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting articles");
            return Result<IEnumerable<ArticleLightDto>>.Failure($"Failed to get articles: {ex.Message}");
        }
    }
}