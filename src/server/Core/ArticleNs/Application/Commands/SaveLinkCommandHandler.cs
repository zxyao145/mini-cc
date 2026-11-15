using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class SaveLinkCommandHandler(
    IUnitOfWork unitOfWork,
    IArticleDomainService articleDomainService,
    IMapper mapper,
    ILogger<SaveLinkCommandHandler> logger) : IRequestHandler<SaveLinkCommand, Result<ArticleDto>>
{
    public async Task<Result<ArticleDto>> Handle(SaveLinkCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            await unitOfWork.LockArticleCreationAsync(cancellationToken);
            var existing = await unitOfWork.Articles.GetByUrlAsync(UrlNormalizer.Normalize(request.Url), cancellationToken);
            existing?.Restore();
            var article = existing ?? articleDomainService.CreateLink(request.Url, request.Title, request.Author ?? "",
                request.Summary ?? "", request.ImageUrl ?? "");
            article.MarkSavedFromExtension(DateTimeOffset.UtcNow);
            if (existing is null)
            {
                article = await unitOfWork.Articles.AddAsync(article, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result<ArticleDto>.Success(mapper.Map<ArticleDto>(article));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            logger.LogError(ex, "Error saving link from URL: {Url}", request.Url);
            return Result<ArticleDto>.Failure($"Failed to save link: {ex.Message}");
        }
    }
}
