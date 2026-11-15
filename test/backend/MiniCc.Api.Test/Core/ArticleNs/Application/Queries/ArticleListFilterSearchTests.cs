using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Queries;

public class ArticleListFilterSearchTests
{
    [Fact]
    public void Apply_RejectsSearchWithoutDatabaseContext()
    {
        var articles = Array.Empty<Article>().AsQueryable();
        var request = new ArticleListRequest { Q = "search" };

        var apply = () => ArticleListFilter.Apply(articles, request);

        apply.Should().Throw<ArgumentException>().WithParameterName("query");
    }
}
