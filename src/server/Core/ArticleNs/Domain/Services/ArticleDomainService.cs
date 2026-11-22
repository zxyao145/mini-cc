using HtmlAgilityPack;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ImageNs.Domain;

namespace MiniCc.Api.Core.ArticleNs.Domain.Services;

public class ArticleDomainService : IArticleDomainService
{
    public Article CreateArticleFromContent(string url, string originalContent, string readableContent,
        string title, string author, string summary, string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(readableContent))
            throw new ArgumentException("Readable content cannot be empty", nameof(readableContent));

        var document = new HtmlDocument();
        document.LoadHtml(readableContent);
        var length = (HtmlEntity.DeEntitize(document.DocumentNode.InnerText) ?? "").Length;
        var content = Content.Create(originalContent, ImageHtml.Normalize(readableContent), length);

        return Article.Create(Url.Create(url), title, author, content, summary, ImageHtml.NormalizeUrl(imageUrl));
    }

    public Article CreateLink(string url, string title, string author, string summary, string imageUrl)
    {
        return Article.CreateLink(Url.Create(url), title, author, summary, ImageHtml.NormalizeUrl(imageUrl));
    }
}
