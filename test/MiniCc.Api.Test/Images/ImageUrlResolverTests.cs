using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Test.Images;

public class ImageUrlResolverTests
{
    private static readonly string Hash = new('a', 64);
    private const string Url = "https://img.test/photo.png";
    private const string Html = $"<img src=\"{Url}\" width=\"320\">";
    private static readonly string ApiUrl = $"/api/images/{Hash}.png";
    private static readonly ArticleImage Done = new(ArticleImageState.Done, Hash: Hash, Ext: "png");

    [Theory]
    [InlineData(ImageDelivery.Authenticated, "", "/api/images/")]
    [InlineData(ImageDelivery.Public, "", "/api/images/")]
    [InlineData(ImageDelivery.Public, "https://media.example.test/", "https://media.example.test/aa/")]
    public void DoneImages_UseConfiguredDelivery(ImageDelivery delivery, string publicBaseUrl, string prefix)
    {
        var resolver = Create(options =>
        {
            options.Delivery = delivery;
            options.PublicBaseUrl = publicBaseUrl;
            options.Storage.Provider = publicBaseUrl.Length == 0 ? ImageStorageProvider.Local : ImageStorageProvider.S3;
        });
        var images = new Dictionary<string, ArticleImage> { [Url] = Done };

        resolver.ResolveUrl(Url, images, savedFromExtension: false).Should().Be($"{prefix}{Hash}.png");
        resolver.ResolveHtml(Html, images).Should().Be($"<img src=\"{prefix}{Hash}.png\" width=\"320\">");
    }

    [Theory]
    [InlineData(ArticleImageState.Pending)]
    [InlineData(ArticleImageState.Failed)]
    public void UnresolvedImages_KeepOriginalUrlAndAddNoReferrer(ArticleImageState state)
    {
        var resolver = Create();
        var images = new Dictionary<string, ArticleImage> { [Url] = new(state) };

        resolver.ResolveUrl(Url, images, savedFromExtension: false).Should().Be(Url);
        resolver.ResolveHtml(Html, images).Should()
            .Be($"<img src=\"{Url}\" width=\"320\" referrerpolicy=\"no-referrer\">");
    }

    [Fact]
    public void LegacyNullImages_StripPrefixInStoreMode()
    {
        var resolver = Create();
        resolver.ResolveUrl(TestImages.Proxy().GetUrl(Url), null, savedFromExtension: true).Should().Be(Url);
        resolver.ResolveHtml(TestImages.LegacyRewrite(Html), null).Should()
            .Be($"<img src=\"{Url}\" width=\"320\" referrerpolicy=\"no-referrer\">");
    }

    [Fact]
    public void ProxyMode_ReturnsLegacyHtmlForNewAndLegacyRows()
    {
        var resolver = Create(options => options.Mode = ImageMode.ImageProxy);
        var oldHtml = TestImages.LegacyRewrite(Html);

        resolver.ResolveHtml(oldHtml, null).Should().Be(oldHtml);
        resolver.ResolveHtml(ImageHtml.Normalize(Html), new Dictionary<string, ArticleImage> { [Url] = Done })
            .Should().Be(oldHtml);
    }

    [Fact]
    public void ProxyMode_LegacyAbsolutePrefix_IsNotSignedAgainBeforeBackfill()
    {
        var oldProxy = new ImageProxyService(Options.Create(new ImageProxyOptions
        {
            ProxyUrl = "https://old-proxy.test/custom/images",
            SignatureKey = "old-signing-key"
        }));
        var oldHtml = oldProxy.RewriteImages(Html);
        var oldCover = oldProxy.GetUrl(Url);
        var resolver = Create(options => options.Mode = ImageMode.ImageProxy);

        resolver.ResolveHtml(oldHtml, null).Should().Be(oldHtml);
        resolver.ResolveUrl(oldCover, null, savedFromExtension: true).Should().Be(oldCover);
        resolver.ResolveUrl(Url, null, savedFromExtension: true).Should().Be(TestImages.Proxy().GetUrl(Url));
        TestImages.Legacy().StripHtml(oldHtml).Should().Be(Html);
        TestImages.Legacy().StripUrl(oldCover).Should().Be(Url);
    }

    [Fact]
    public void ResolveHtml_ResolvesEntityEncodedAndUnicodeUrls()
    {
        const string address = "https://例子.测试/图/a.png?x=1&y=2";
        var images = new Dictionary<string, ArticleImage> { [ImageHtml.NormalizeUrl(address)] = Done };
        var html = ImageHtml.Normalize("""<img src="https://例子.测试/图/a.png?x=1&amp;y=2">""");

        Create().ResolveHtml(html, images).Should().Be($"<img src=\"{ApiUrl}\">");
    }

    [Fact]
    public void ResolveHtml_ReplacesEveryOccurrence()
    {
        var html = $"""
            <picture><source srcset="{Url} 2x"><img src="{Url}"></picture>
            <img srcset="{Url} 3x">
            """;
        Create().ResolveHtml(html, new Dictionary<string, ArticleImage> { [Url] = Done })
            .Should().Be(html.Replace(Url, ApiUrl));
    }

    [Fact]
    public void ResolveUrl_EmptyCover_ReturnsEmpty() => Create().ResolveUrl("", null, savedFromExtension: false).Should().BeEmpty();

    [Fact]
    public void ImageKeys_ParseAndFormat()
    {
        var key = ImageKeys.For(Hash, "png");
        key.Should().Be($"aa/{Hash}.png");
        ImageKeys.TryParseKey(key, out var hash, out var ext).Should().BeTrue();
        hash.Should().Be(Hash);
        ext.Should().Be("png");
        ImageKeys.TryParseName(key[3..], out _, out _).Should().BeTrue();
        ImageKeys.TryParseKey("ab/x", out _, out _).Should().BeFalse();
        ImageKeys.TryParseKey("zz/" + key[3..], out _, out _).Should().BeFalse();
        ImageKeys.TryParseKey("ab/" + key[3..], out _, out _).Should().BeFalse();
        ImageKeys.TryParseName(Hash.ToUpperInvariant() + ".png", out _, out _).Should().BeFalse();
        ImageKeys.TryParseName(Hash + ".exe", out _, out _).Should().BeFalse();
    }

    private static ImageUrlResolver Create(Action<ImageOptions>? configure = null)
    {
        var options = new ImageOptions();
        configure?.Invoke(options);
        return new(Options.Create(options), TestImages.Proxy(), TestImages.Legacy());
    }
}
