using MediatR;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
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
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _unitOfWork.LockArticleCreationAsync(cancellationToken);
            var existingArticle = await _unitOfWork.Articles.GetByUrlAsync(UrlNormalizer.Normalize(request.Url), cancellationToken);
            if (existingArticle != null)
            {
                existingArticle.Restore();
                existingArticle.MarkSavedFromExtension(DateTimeOffset.UtcNow);
                var replacement = _articleDomainService.CreateArticleFromContent(request.Url, request.OriginalContent,
                    request.ReadableContent, request.Title ?? existingArticle.Title, request.Author ?? "", request.Summary ?? "", request.ImageUrl ?? "");
                var wasFull = existingArticle.ContentKind == ContentKind.Full;
                if (!wasFull || replacement.ReadableContent != existingArticle.ReadableContent)
                {
                    existingArticle.ReplaceContent(Content.Create(replacement.OriginalContent, replacement.ReadableContent, replacement.Length), ContentKind.Full);
                    existingArticle.ResetProgress(null, NextProgressVersion(existingArticle));
                    foreach (var highlight in existingArticle.Highlights) highlight.AnchorStatus = AnchorStatus.Pending;
                }
                if (wasFull) existingArticle.RefreshMetadata(replacement.Title, replacement.Author, replacement.Summary, replacement.ImageUrl);
                else existingArticle.FillMissingMetadata(replacement.Author, replacement.Summary, replacement.ImageUrl);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result<ArticleDto>.Success(_mapper.Map<ArticleDto>(existingArticle));
            }

            var article = _articleDomainService
                .CreateArticleFromContent(
                request.Url,
                request.OriginalContent,
                request.ReadableContent,
                request.Title ?? "",
                request.Author ?? "",
                request.Summary ?? "",
                request.ImageUrl ?? ""
                );
            article.MarkSavedFromExtension(DateTimeOffset.UtcNow);
            var savedArticle = await _unitOfWork.Articles.AddAsync(article, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result<ArticleDto>.Success(_mapper.Map<ArticleDto>(savedArticle));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            _logger.LogError(ex, "Error saving article from URL: {Url}", request.Url);
            return Result<ArticleDto>.Failure($"Failed to save article: {ex.Message}");
        }
    }

    private static DateTimeOffset NextProgressVersion(Article article)
    {
        var clock = DateTimeOffset.UtcNow;
        var version = clock.AddTicks(-clock.Ticks % 10);
        return article.ProgressUpdatedAt >= version ? article.ProgressUpdatedAt.Value.AddTicks(10) : version;
    }
}
