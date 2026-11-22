using HtmlAgilityPack;
using MiniCc.Api.Core.ImageNs.Domain;

namespace MiniCc.Api.Test.Images;

public class ImageHtmlTests
{
    public static TheoryData<string> Fixtures => new()
    {
        """<img src="https://example.com/photo.png?x=1&amp;y=2" width="320" height="180">""",
        """<img data-src="https://mmbiz.qpic.cn/pic.png" src="https://mmbiz.qpic.cn/pic.png" crossorigin="anonymous">""",
        """<picture><source srcset="https://pic.zhimg.com/a,a.png 480w, https://example.com/large.png 960w"><img src="data:image/png;base64,AAAA" srcset="data:image/png;base64,AAAA 1x, /retina.png 2x"></picture>""",
        """<video><source src="https://example.com/movie.mp4"></video><audio><source src="https://example.com/song.ogg"></audio>""",
        """<img src="https://例子.测试/图/a.png?x=1&amp;copy;=2" srcset="2x, https://example.com/原图.png 4x">""",
        """<p>No images</p>""",
    };

    [Theory, MemberData(nameof(Fixtures))]
    public void Normalize_EqualsLegacyRewriteWithPrefixStripped(string html) =>
        ImageHtml.Normalize(html).Should().Be(TestImages.Legacy().StripHtml(TestImages.LegacyRewrite(html)));

    [Theory, MemberData(nameof(Fixtures))]
    public void ProxyRewriteOfNormalized_EqualsLegacyRewrite(string html) =>
        TestImages.Proxy().RewriteImages(ImageHtml.Normalize(html)).Should().Be(TestImages.LegacyRewrite(html));

    [Fact]
    public void Normalize_KeepsDataAndRelativeUrls_RemovesCrossorigin() =>
        ImageHtml.Normalize("""<img src="data:image/png;base64,AAAA" srcset="/cover.png 2x" crossorigin="anonymous">""")
            .Should().Be("""<img src="data:image/png;base64,AAAA" srcset="/cover.png 2x">""");

    [Fact]
    public void CollectDownloadUrls_IncludesPictureAndBodyUrlsOnceAndExcludesMedia()
    {
        const string html = """
            <video><source src="https://a.test/video.mp4"></video>
            <audio><source src="https://a.test/audio.ogg"></audio>
            <picture><source src="https://a.test/picture.png" srcset="https://a.test/b.png 3x">
            <source srcset="https://a.test/large.png 2x">
            <img src="https://a.test/a.png" srcset="/ignore.png 1x, https://a.test/c.png 2x"></picture>
            <img src="https://a.test/a.png">
            """;
        ImageHtml.CollectDownloadUrls(html, "https://a.test/a.png").Should().Equal(
            "https://a.test/picture.png", "https://a.test/b.png", "https://a.test/large.png", "https://a.test/a.png", "https://a.test/c.png");
    }

    [Fact]
    public void Rewrite_NeedsNoReferrer_MarksImgAndPictureImg()
    {
        const string html = """
            <picture><source srcset="https://a.test/x.png 2x"><img src="/cover.png"></picture>
            <img src="https://a.test/y.png"><img src="/z.png">
            """;
        var result = ImageHtml.Rewrite(html, (url, _, _) => url, (url, _) => url.StartsWith("https://"));
        var document = new HtmlDocument();
        document.LoadHtml(result);
        document.DocumentNode.Descendants("img").Select(img => img.GetAttributeValue("referrerpolicy", ""))
            .Should().Equal("no-referrer", "no-referrer", "");
    }
}
