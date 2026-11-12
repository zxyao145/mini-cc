using MediatR;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data;
using System.Linq.Expressions;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class GetArticleListQueryHandler(MiniCcDbContext db, ImageProxyService images)
    : IRequestHandler<GetArticleListQuery, Result<ArticleListPage>>
{
    public async Task<Result<ArticleListPage>> Handle(GetArticleListQuery query, CancellationToken cancellationToken)
    {
        var request = query.Request;
        try
        {
            var filtered = ArticleListFilter.Apply(db.Articles.AsNoTracking(), request);
            var sort = request.EffectiveSort;
            var cursor = request.Cursor == null ? null : ArticleCursor.Decode(request.Cursor, sort);
            var total = await filtered.CountAsync(cancellationToken);
            Expression<Func<Article, DateTimeOffset?>> key = sort switch
            {
                "read_desc" => a => a.LastReadAt,
                "published_desc" => a => a.PublishedAt,
                "trash" => a => a.DeletedAt,
                _ => a => a.CreatedAt
            };
            if (cursor != null) filtered = ApplyCursor(filtered, key, cursor, sort == "saved_asc");
            var ordered = sort == "saved_asc"
                ? filtered.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
                : filtered.OrderBy(NullKey(key)).ThenByDescending(key).ThenByDescending(a => a.Id);
            var items = await ordered.Take(request.PageSize + 1).Select(ArticleListItemDto.Projection).ToListAsync(cancellationToken);
            var hasMore = items.Count > request.PageSize;
            if (hasMore) items.RemoveAt(items.Count - 1);
            var last = items.LastOrDefault();
            var next = hasMore && last != null ? ArticleCursor.Encode(sort, sort switch
            {
                "read_desc" => last.LastReadAt, "published_desc" => last.PublishedAt, "trash" => last.DeletedAt, _ => last.CreatedAt
            }, last.Id) : null;
            foreach (var item in items) item.ImageUrl = images.GetUrl(item.ImageUrl);
            return Result<ArticleListPage>.Success(new(items, total, next));
        }
        catch (ArgumentException ex) { return Result<ArticleListPage>.Failure(ex.Message); }
    }

    private static Expression<Func<Article, bool>> NullKey(Expression<Func<Article, DateTimeOffset?>> key) =>
        Expression.Lambda<Func<Article, bool>>(Expression.Equal(key.Body, Expression.Constant(null, typeof(DateTimeOffset?))), key.Parameters);

    private static IQueryable<Article> ApplyCursor(IQueryable<Article> query, Expression<Func<Article, DateTimeOffset?>> key, ArticleCursor cursor, bool ascending)
    {
        var value = Expression.Constant(cursor.Value, typeof(DateTimeOffset?));
        var id = Expression.Property(key.Parameters[0], nameof(Article.Id));
        var compareId = Expression.Call(id, nameof(Guid.CompareTo), Type.EmptyTypes, Expression.Constant(cursor.Id));
        var idAfter = ascending ? Expression.GreaterThan(compareId, Expression.Constant(0)) : Expression.LessThan(compareId, Expression.Constant(0));
        var sameKey = Expression.AndAlso(Expression.Equal(key.Body, value), idAfter);
        Expression after = cursor.Value == null ? sameKey : Expression.OrElse(
            Expression.OrElse(ascending ? Expression.GreaterThan(key.Body, value) : Expression.LessThan(key.Body, value), sameKey),
            Expression.Equal(key.Body, Expression.Constant(null, typeof(DateTimeOffset?))));
        return query.Where(Expression.Lambda<Func<Article, bool>>(after, key.Parameters));
    }
}
