using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.ImageNs.Infrastructure.Persistence;

public sealed class ImageArticleRows(MiniCcDbContext db, IUnitOfWork unitOfWork) : IImageArticleRows
{
    public async Task<int> UpdateLockedAsync(Guid id, Func<Article, int> update, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var article = await db.Articles.FromSqlForUpdate($"""SELECT * FROM "Articles" WHERE "Id" = {id}""")
                .IgnoreQueryFilters().SingleOrDefaultAsync(cancellationToken);
            var changed = article is null ? 0 : update(article);
            if (changed == 0)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return 0;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return changed;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
