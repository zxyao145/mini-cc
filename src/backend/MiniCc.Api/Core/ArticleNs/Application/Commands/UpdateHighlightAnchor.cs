using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;
namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record UpdateHighlightAnchor(Guid Id, int? StartOffset, int? EndOffset, bool Orphaned, int? ContentRevision = null)
    : IRequest<Result<HighlightSave>>;
public class UpdateHighlightAnchorValidator : AbstractValidator<UpdateHighlightAnchor>
{
    public UpdateHighlightAnchorValidator()
    { RuleFor(r => r.StartOffset).NotNull().GreaterThanOrEqualTo(0).When(r => !r.Orphaned); RuleFor(r => r.EndOffset).NotNull().GreaterThan(r => r.StartOffset).When(r => !r.Orphaned); }
}
public class UpdateHighlightAnchorHandler(MiniCcDbContext db, IUnitOfWork unitOfWork, IMapper mapper)
    : IRequestHandler<UpdateHighlightAnchor, Result<HighlightSave>>
{
    public async Task<Result<HighlightSave>> Handle(UpdateHighlightAnchor r, CancellationToken ct)
    {
        var articleId = await db.Highlights.Where(h => h.Id == r.Id)
            .Select(h => (Guid?)h.ArticleId).SingleOrDefaultAsync(ct);
        if (!articleId.HasValue) return Result<HighlightSave>.Failure("not found");

        // Share the article lock with body replacement, then read the highlight in a fresh statement.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var article = await unitOfWork.Articles.GetByIdForProgressUpdateAsync(articleId.Value, ct);
        if (article == null) return Result<HighlightSave>.Failure("not found");

        var highlight = await db.Highlights.SingleOrDefaultAsync(h => h.Id == r.Id, ct);
        if (highlight == null) return Result<HighlightSave>.Failure("not found");
        if (r.ContentRevision.HasValue && r.ContentRevision != article.ContentRevision)
            return Result<HighlightSave>.Success(new(ConflictingRevision: article.ContentRevision));

        if (highlight.AnchorStatus == AnchorStatus.Pending)
        {
            highlight.AnchorStatus = r.Orphaned ? AnchorStatus.Orphaned : AnchorStatus.Ok;
            if (!r.Orphaned) { highlight.StartOffset = r.StartOffset!.Value; highlight.EndOffset = r.EndOffset!.Value; }
            await unitOfWork.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Result<HighlightSave>.Success(new(mapper.Map<HighlightDto>(highlight)));
    }
}
