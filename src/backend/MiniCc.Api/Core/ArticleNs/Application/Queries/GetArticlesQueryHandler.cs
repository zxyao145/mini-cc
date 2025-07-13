using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetArticlesQueryHandler : IRequestHandler<GetArticlesQuery, Result<IEnumerable<ArticleDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetArticlesQueryHandler> _logger;

    public GetArticlesQueryHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetArticlesQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<IEnumerable<ArticleDto>>> Handle(GetArticlesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var articles = await _unitOfWork.Articles.GetAllAsync(
                request.Page, request.PageSize, request.Search, cancellationToken);

            var articleDtos = _mapper.Map<IEnumerable<ArticleDto>>(articles);

            return Result<IEnumerable<ArticleDto>>.Success(articleDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting articles");
            return Result<IEnumerable<ArticleDto>>.Failure($"Failed to get articles: {ex.Message}");
        }
    }
}