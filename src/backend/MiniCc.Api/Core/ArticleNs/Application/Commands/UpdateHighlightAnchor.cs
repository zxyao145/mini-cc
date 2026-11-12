using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;
namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record UpdateHighlightAnchor(Guid Id, int? StartOffset, int? EndOffset, bool Orphaned) : IRequest<Result<HighlightDto>>;
public class UpdateHighlightAnchorValidator : AbstractValidator<UpdateHighlightAnchor>
{
    public UpdateHighlightAnchorValidator()
    { RuleFor(r => r.StartOffset).NotNull().GreaterThanOrEqualTo(0).When(r => !r.Orphaned); RuleFor(r => r.EndOffset).NotNull().GreaterThan(r => r.StartOffset).When(r => !r.Orphaned); }
}
public class UpdateHighlightAnchorHandler(MiniCcDbContext db, IUnitOfWork unitOfWork, IMapper mapper) : IRequestHandler<UpdateHighlightAnchor, Result<HighlightDto>>
{
    public async Task<Result<HighlightDto>> Handle(UpdateHighlightAnchor r, CancellationToken ct)
    {
        var highlight = await db.Highlights.SingleOrDefaultAsync(h => h.Id == r.Id, ct);
        if (highlight == null) return Result<HighlightDto>.Failure("not found");
        if (highlight.AnchorStatus == AnchorStatus.Pending)
        {
            highlight.AnchorStatus = r.Orphaned ? AnchorStatus.Orphaned : AnchorStatus.Ok;
            if (!r.Orphaned) { highlight.StartOffset = r.StartOffset!.Value; highlight.EndOffset = r.EndOffset!.Value; }
            await unitOfWork.SaveChangesAsync(ct);
        }
        return Result<HighlightDto>.Success(mapper.Map<HighlightDto>(highlight));
    }
}
