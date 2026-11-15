using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ArticleNs.Infrastructure;

internal static class ArticleSearch
{
    public static IQueryable<Article> Apply(IQueryable<Article> query, DatabaseFacade database, string search)
    {
        var text = search.Trim();
        var pattern = "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        if (!database.IsSqlite())
            return query.Where(a => a.SearchVector.Matches(EF.Functions.PlainToTsQuery("mixed_zh_en", text))
                || EF.Functions.ILike(a.Title, pattern, "\\"));

        return query.Where(a => EF.Functions.Like(a.Title, pattern, "\\")
            || EF.Functions.Like(a.Summary, pattern, "\\")
            || EF.Functions.Like(a.Author, pattern, "\\")
            || EF.Functions.Like(a.ReadableContent, pattern, "\\"));
    }
}
