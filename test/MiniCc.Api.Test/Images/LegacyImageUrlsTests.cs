namespace MiniCc.Api.Test.Images;

public class LegacyImageUrlsTests
{
    [Theory]
    [InlineData("/imageProxy", "/imageProxy/320x180,sAbC-_/https://a.test/x.png?q=1")]
    [InlineData("http://localhost:5050/", "http://localhost:5050/0x0,sX/https://a.test/x.png?q=1")]
    [InlineData("/custom", "/custom/0x0,sX/https://a.test/x.png?q=1")]
    [InlineData("/custom", "/imageProxy/0x0,sX/https://a.test/x.png?q=1")]
    public void StripUrl_RemovesKnownProxyPrefixes(string prefix, string url) =>
        TestImages.Legacy(prefix).StripUrl(url).Should().Be("https://a.test/x.png?q=1");

    [Theory]
    [InlineData("/imageProxy/notsigned/https://a.test")]
    [InlineData("https://a.test/imageProxy/0x0,sX/https://b")]
    public void StripUrl_NonMatching_Unchanged(string url) =>
        TestImages.Legacy().StripUrl(url).Should().Be(url);

    [Theory]
    [InlineData("https://old-minicc.test")]
    [InlineData("https://old-minicc.test/images")]
    public void StripUrl_HistoricalAbsoluteProxy_StripsOnlyTheOuterLayer(string prefix)
    {
        var source = TestImages.Proxy(proxyUrl: "https://feed-images.test/cache").GetUrl("https://example.test/photo.png");
        var wrapped = TestImages.Proxy(proxyUrl: prefix).GetUrl(source);

        TestImages.Legacy().StripUrl(wrapped).Should().Be(source);
    }

    [Fact]
    public void StripHtml_IgnoresTextContainingPrefix()
    {
        const string html = "<p>/imageProxy/0x0,sX/https://a.test</p>";
        TestImages.Legacy().StripHtml(html).Should().Be(html);
    }
}
