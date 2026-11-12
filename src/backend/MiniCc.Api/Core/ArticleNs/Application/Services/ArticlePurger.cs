using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ArticleNs.Application.Services;

// All permanent deletion (manual, empty trash and retention) passes through this transaction.
public class ArticlePurger(MiniCcDbContext db, IUnitOfWork unitOfWork)
{
    public async Task<string?> PurgeAsync(Guid id, DateTimeOffset? expiredBefore, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.LockArticleCreationAsync(cancellationToken);
            var article = await db.Articles.FromSqlInterpolated($"SELECT * FROM \"Articles\" WHERE \"Id\" = {id} FOR UPDATE")
                .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            string? reason = article == null ? "notFound" : article.DeletedAt == null ? "notInTrash"
                : expiredBefore.HasValue && article.DeletedAt > expiredBefore ? "notExpired" : null;
            if (reason == null)
            {
                await db.FeedEntries.Where(e => e.ArticleId == id).ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Dismissed, true).SetProperty(e => e.ArticleId, (Guid?)null), cancellationToken);
                var tracked = db.Articles.Local.FirstOrDefault(a => a.Id == id);
                if (tracked != null) db.Entry(tracked).State = EntityState.Detached;
                db.Articles.Remove(article!);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return reason;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
