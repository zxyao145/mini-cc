using FluentValidation;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class UpdateProgressCommand : IRequest<Result<ProgressSaveDto>>
{
    [JsonIgnore] public Guid ArticleId { get; set; }
    [JsonIgnore] public bool Reset { get; set; }
    public int? TextOffset { get; set; }
    public int BlockIndex { get; set; }
    public double BlockRatio { get; set; }
    public double Percent { get; set; }
    public string SessionId { get; set; } = "";
    public DateTimeOffset? BaseUpdatedAt { get; set; }
}

public class ResetProgressRequest
{
    public string SessionId { get; set; } = "";
}

public record ProgressSnapshot(int? TextOffset, int? BlockIndex, double? BlockRatio, double? Percent,
    DateTimeOffset? ProgressUpdatedAt);
public record ProgressSaveDto(DateTimeOffset ProgressUpdatedAt, ReadingStatus ReadingStatus,
    DateTimeOffset? LastReadAt, ProgressSnapshot? Current = null, ContentKind? ContentKind = null);

public class UpdateProgressCommandValidator : AbstractValidator<UpdateProgressCommand>
{
    public UpdateProgressCommandValidator()
    {
        RuleFor(c => c.ArticleId).NotEmpty();
        RuleFor(c => c.SessionId).NotEmpty().MaximumLength(64);
        When(c => !c.Reset, () =>
        {
            RuleFor(c => c.TextOffset).GreaterThanOrEqualTo(0).When(c => c.TextOffset.HasValue);
            RuleFor(c => c.BlockIndex).GreaterThanOrEqualTo(0);
            RuleFor(c => c.BlockRatio).InclusiveBetween(0, 1);
            RuleFor(c => c.Percent).InclusiveBetween(0, 100);
        });
    }
}

public class UpdateProgressCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProgressCommand, Result<ProgressSaveDto>>
{
    public async Task<Result<ProgressSaveDto>> Handle(UpdateProgressCommand request, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var article = await unitOfWork.Articles.GetByIdForProgressUpdateAsync(request.ArticleId, cancellationToken);
            if (article == null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<ProgressSaveDto>.Failure("article not found");
            }
            if (!request.Reset && article.ProgressUpdatedAt.HasValue && article.ProgressSessionId != request.SessionId &&
                (!request.BaseUpdatedAt.HasValue || request.BaseUpdatedAt < article.ProgressUpdatedAt))
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result<ProgressSaveDto>.Success(new(article.ProgressUpdatedAt.Value, article.ReadingStatus,
                    article.LastReadAt, new(article.ProgressTextOffset, article.ProgressBlockIndex, article.ProgressBlockRatio,
                        article.ProgressPercent, article.ProgressUpdatedAt)));
            }
            // Preserve a strictly newer server version even for writes inside the same clock tick.
            var clock = DateTimeOffset.UtcNow;
            var now = new DateTimeOffset(clock.Ticks - clock.Ticks % 10, TimeSpan.Zero);
            if (article.ProgressUpdatedAt >= now) now = article.ProgressUpdatedAt.Value.AddTicks(10);
            if (request.Reset) article.ResetProgress(request.SessionId, now);
            else article.ApplyProgress(new ReadingPosition(request.TextOffset, request.BlockIndex, request.BlockRatio, request.Percent), request.SessionId, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result<ProgressSaveDto>.Success(new(now, article.ReadingStatus, article.LastReadAt, ContentKind: article.ContentKind));
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
