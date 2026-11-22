using MiniCc.Api.Core.ArticleNs.Domain.Services;

namespace MiniCc.Api.Test.Core.ArticleNs.Domain.Services;

public class ArticleDomainServiceTests
{
    private readonly ArticleDomainService _service = new();

    [Fact]
    public void CreateArticleFromContent_StoresExtensionOutputAndCountsDecodedText()
    {
        var article = _service.CreateArticleFromContent("https://example.com/article", "<html>Original page</html>",
            "<p>Hello &amp; world</p>", "Article title", "Author", "Summary", "");

        article.OriginalContent.Should().Be("<html>Original page</html>");
        article.ReadableContent.Should().Be("<p>Hello &amp; world</p>");
        article.Length.Should().Be("Hello & world".Length);
        article.Title.Should().Be("Article title");
        article.Author.Should().Be("Author");
        article.Summary.Should().Be("Summary");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateArticleFromContent_RejectsEmptyExtractedContent(string readableContent)
    {
        var act = () => _service.CreateArticleFromContent("https://example.com/article", "Original", readableContent,
            "Title", "", "", "");

        act.Should().Throw<ArgumentException>().WithParameterName("readableContent");
    }

    [Fact]
    public void CreateArticleFromContent_NormalizesCoverImageWithoutChangingOriginalHtml()
    {
        var article = _service.CreateArticleFromContent("https://example.com/article", "<img src='original.png'>",
            "<p>Readable</p>", "Title", "", "", "https://example.com/cover.png");

        article.ImageUrl.Should().Be("https://example.com/cover.png");
        article.OriginalContent.Should().Be("<img src='original.png'>");
    }
}
