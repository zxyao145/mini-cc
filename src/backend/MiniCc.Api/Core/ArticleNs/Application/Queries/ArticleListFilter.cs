using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public static class ArticleListFilter
{
    public static IQueryable<Article> Apply(IQueryable<Article> query, ArticleListRequest request, bool useLikeSearch = false)
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
            var text = request.Q.Trim();
            var pattern = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            if (useLikeSearch)
                query = query.Where(a => EF.Functions.Like(a.Title, pattern, "\\")
                    || EF.Functions.Like(a.Summary, pattern, "\\")
                    || EF.Functions.Like(a.Author, pattern, "\\")
                    || EF.Functions.Like(a.ReadableContent, pattern, "\\"));
            else
                query = query.Where(a => a.SearchVector.Matches(EF.Functions.PlainToTsQuery("mixed_zh_en", text)) || EF.Functions.ILike(a.Title, pattern, "\\"));
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
