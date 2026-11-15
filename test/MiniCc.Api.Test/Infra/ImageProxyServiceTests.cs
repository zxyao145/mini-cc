using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MiniCc.Api.Infra;
using System.Security.Cryptography;
using System.Text;

namespace MiniCc.Api.Test.Infra;

public class ImageProxyServiceTests
{
    private readonly ImageProxyService _service = new(Options.Create(new ImageProxyOptions
    {
        ProxyUrl = "/imageProxy",
        SignatureKey = "local-test-secret"
    }));

    [Fact]
    public void RewriteImages_SignsDecodedImageUrlAndDimensions()
    {
        var html = "<img src=\"https://example.com/photo.png?x=1&amp;y=2\" width=\"320\" height=\"180\">";

        var result = _service.RewriteImages(html);
        var document = new HtmlDocument();
        document.LoadHtml(result);
        var url = HtmlEntity.DeEntitize(document.DocumentNode.SelectSingleNode("//img")!.GetAttributeValue("src", ""));

        url.Should().Be(SignedUrl("https://example.com/photo.png?x=1&y=2", "320x180"));
        result.Should().NotContain("local-test-secret");
    }

    [Fact]
    public void RewriteImages_ProxiesPictureSourcesAndPreservesDescriptorsAndDataImages()
    {
        var html = "<picture><source srcset=\"https://example.com/small.png 480w, https://example.com/large.png 960w\"><img src=\"data:image/png;base64,AAAA\" srcset=\"data:image/png;base64,AAAA 1x, https://example.com/retina.png 2x\"></picture>";

        var document = new HtmlDocument();
        document.LoadHtml(_service.RewriteImages(html));

        document.DocumentNode.SelectSingleNode("//source")!.GetAttributeValue("srcset", "")
            .Should().Be($"{SignedUrl("https://example.com/small.png")} 480w, {SignedUrl("https://example.com/large.png")} 960w");
        document.DocumentNode.SelectSingleNode("//img")!.GetAttributeValue("src", "")
            .Should().Be("data:image/png;base64,AAAA");
        document.DocumentNode.SelectSingleNode("//img")!.GetAttributeValue("srcset", "")
            .Should().Be($"data:image/png;base64,AAAA 1x, {SignedUrl("https://example.com/retina.png")} 2x");
    }

    [Fact]
    public void RewriteImages_DoesNotSignExistingProxyUrlsAgain()
    {
        var once = _service.RewriteImages("<img src=\"https://example.com/photo.png\">");

        _service.RewriteImages(once).Should().Be(once);
    }

    [Fact]
    public void GetUrl_SupportsAbsoluteProxyBaseForLocalDevelopment()
    {
        var service = new ImageProxyService(Options.Create(new ImageProxyOptions
        {
            ProxyUrl = "http://localhost:5050/",
            SignatureKey = "local-test-secret"
        }));

        service.GetUrl("https://example.com/photo.png")
            .Should().Be("http://localhost:5050" + SignedUrl("https://example.com/photo.png")["/imageProxy".Length..]);
    }

    [Fact]
    public void RewriteImages_DecodesEntitiesOnceAndEscapesSignedAttributes()
    {
        const string url = "https://example.com/photo.png?x=1&copy;=2";
        var html = "<img src=\"https://example.com/photo.png?x=1&amp;copy;=2\" srcset=\"https://example.com/photo.png?x=1&amp;copy;=2 2x\">";
        var document = new HtmlDocument();
        document.LoadHtml(_service.RewriteImages(html));
        var image = document.DocumentNode.SelectSingleNode("//img")!;

        HtmlEntity.DeEntitize(image.GetAttributeValue("src", "")).Should().Be(SignedUrl(url));
        HtmlEntity.DeEntitize(image.GetAttributeValue("srcset", "")).Should().Be(SignedUrl(url) + " 2x");
        _service.GetUrl(url).Should().Be(SignedUrl(url));
    }

    private static string SignedUrl(string url, string dimensions = "0x0")
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes("local-test-secret"), Encoding.UTF8.GetBytes($"{url}#{dimensions}"));
        var signature = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"/imageProxy/{dimensions},s{signature}/{url}";
    }
}
