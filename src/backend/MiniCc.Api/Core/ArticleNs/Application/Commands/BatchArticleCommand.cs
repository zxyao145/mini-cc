using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Application.Services;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class BatchArticleCommand : IRequest<Result<BatchArticleResult>>
{
    public Guid[] Ids { get; set; } = [];
    public string Action { get; set; } = "";
    public Guid[] TagIds { get; set; } = [];
}
public record BatchArticleFailure(Guid Id, string Reason);
public record BatchArticleResult(List<Guid> Succeeded, List<BatchArticleFailure> Failed);
public class BatchArticleCommandValidator : AbstractValidator<BatchArticleCommand>
{
    public static readonly string[] Actions = ["markRead", "markUnread", "favorite", "unfavorite", "archive", "unarchive", "addTags", "removeTags", "trash", "restore", "purge"];
    public BatchArticleCommandValidator()
    {
        RuleFor(c => c.Ids).NotNull().Must(ids => ids != null && ids.Distinct().Count() is >= 1 and <= 200);
        RuleForEach(c => c.Ids).NotEmpty();
        RuleFor(c => c.Action).Must(Actions.Contains);
        RuleFor(c => c.TagIds).NotNull().Must(ids => ids != null && ids.Distinct().Count() is >= 1 and <= 20)
            .When(c => c.Action is "addTags" or "removeTags");
        RuleForEach(c => c.TagIds).NotEmpty().When(c => c.Action is "addTags" or "removeTags");
    }
}
public class BatchArticleCommandHandler(MiniCcDbContext db, IUnitOfWork unitOfWork, ArticlePurger purger, ILogger<BatchArticleCommandHandler> logger)
    : IRequestHandler<BatchArticleCommand, Result<BatchArticleResult>>
{
    public async Task<Result<BatchArticleResult>> Handle(BatchArticleCommand request, CancellationToken cancellationToken)
    {
        var tagIds = request.TagIds?.Distinct().ToArray() ?? [];
        if (request.Action is "addTags" or "removeTags" && await db.Tags.CountAsync(t => tagIds.Contains(t.Id), cancellationToken) != tagIds.Length)
            return Result<BatchArticleResult>.Failure("tag not found");
        var result = new BatchArticleResult([], []);
        foreach (var id in request.Ids.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                db.ChangeTracker.Clear();
                string? reason;
                if (request.Action == "purge") reason = await purger.PurgeAsync(id, null, cancellationToken);
                else
                {
                    var article = await db.Articles.IgnoreQueryFilters().Include(a => a.Tags).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
                    reason = article == null ? "notFound" : request.Action == "restore"
                        ? article.DeletedAt == null ? "notInTrash" : null : article.DeletedAt != null ? "inTrash" : null;
                    if (reason == null)
                    {
                        switch (request.Action)
                        {
                            case "markRead": article!.MarkRead(DateTimeOffset.UtcNow); break;
                            case "markUnread": article!.MarkUnread(); break;
                            case "favorite": article!.SetFavorite(true); break;
                            case "unfavorite": article!.SetFavorite(false); break;
                            case "archive": article!.SetArchived(true); break;
                            case "unarchive": article!.SetArchived(false); break;
                            case "trash": article!.MoveToTrash(DateTimeOffset.UtcNow); break;
                            case "restore": article!.Restore(); break;
                            case "addTags":
                                foreach (var tag in await db.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync(cancellationToken)) article!.AddTag(tag);
                                break;
                            case "removeTags": foreach (var tagId in tagIds) article!.RemoveTag(tagId); break;
                        }
                        await unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                }
                if (reason == null) result.Succeeded.Add(id); else result.Failed.Add(new(id, reason));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                db.ChangeTracker.Clear();
                logger.LogError(ex, "Batch {Action} failed for article {ArticleId}", request.Action, id);
                result.Failed.Add(new(id, "error"));
            }
        }
        return Result<BatchArticleResult>.Success(result);
    }
}
