using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Infrastructure;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public static class ArticleListFilter
{
    public static IQueryable<Article> Apply(MiniCcDbContext db, ArticleListRequest request) =>
        Apply(db.Articles.AsNoTracking(), request, db.Database);

    public static IQueryable<Article> Apply(IQueryable<Article> query, ArticleListRequest request) =>
        Apply(query, request, null);

    private static IQueryable<Article> Apply(IQueryable<Article> query, ArticleListRequest request, DatabaseFacade? database)
    {
        request.Validate();
        query = request.View switch
        {
            "inbox" => query.Where(a => !a.IsArchived),
            "favorites" => query.Where(a => a.IsFavorite),
            "archive" => query.Where(a => a.IsArchived),
            "trash" => query.IgnoreQueryFilters().Where(a => a.DeletedAt != null),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var searchDatabase = database ?? throw new ArgumentException("Article search requires a database context.", nameof(query));
            query = ArticleSearch.Apply(query, searchDatabase, request.Q);
        }
        var statuses = request.Statuses;
        // C# 14 can bind array.Contains to the span overload, which EF Core 9 cannot evaluate in this expression.
        if (statuses.Length > 0) query = query.Where(a => Enumerable.Contains(statuses, a.ReadingStatus));
        foreach (var id in request.TagIds) query = query.Where(a => a.Tags.Any(t => t.Id == id));
        if (request.View == "feeds" || request.Source == "feed") query = query.Where(a => a.FeedEntries.Any());
        if (request.FeedId != null) query = query.Where(a => a.FeedEntries.Any(e => e.FeedId == request.FeedId));
        if (request.Source == "extension") query = query.Where(a => a.SavedFromExtensionAt != null);
        if (request.SavedFrom.HasValue) query = query.Where(a => a.CreatedAt >= request.SavedFrom.Value);
        if (request.SavedTo.HasValue) query = query.Where(a => a.CreatedAt < request.SavedTo.Value);
        return query;
    }
}
