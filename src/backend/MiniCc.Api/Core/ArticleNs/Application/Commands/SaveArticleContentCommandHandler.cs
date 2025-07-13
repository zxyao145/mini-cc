using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class SaveArticleContentCommandHandler : IRequestHandler<SaveArticleContentCommand, Result<ArticleDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IArticleDomainService _articleDomainService;
    private readonly IMapper _mapper;
    private readonly ILogger<SaveArticleContentCommandHandler> _logger;

    public SaveArticleContentCommandHandler(
        IUnitOfWork unitOfWork,
        IArticleDomainService articleDomainService,
        IMapper mapper,
        ILogger<SaveArticleContentCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _articleDomainService = articleDomainService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<ArticleDto>> Handle(SaveArticleContentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingArticle = await _unitOfWork.Articles.GetByUrlAsync(request.Url, cancellationToken);
            if (existingArticle != null)
            {
                return Result<ArticleDto>.Success(_mapper.Map<ArticleDto>(existingArticle));
            }

            var article = await _articleDomainService
                .CreateArticleFromContentAsync(
                request.Url,
                request.OriginalContent,
                request.Title,
                request.Author,
                request.Summary,
                request.ImageUrl
                );
            var savedArticle = await _unitOfWork.Articles.AddAsync(article, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<ArticleDto>.Success(_mapper.Map<ArticleDto>(savedArticle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving article from URL: {Url}", request.Url);
            return Result<ArticleDto>.Failure($"Failed to save article: {ex.Message}");
        }
    }
}
