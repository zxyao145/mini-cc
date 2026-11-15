using MiniCc.Api.Core.ArticleNs.Domain.Services;

namespace MiniCc.Api.Test.Core.ArticleNs.Domain.Services;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("HTTP://Example.COM:80/path/#part", "http://example.com/path")]
    [InlineData("HTTPS://Example.COM:443/", "https://example.com/")]
    [InlineData("https://example.com:8443/a/", "https://example.com:8443/a")]
    [InlineData("https://example.com/a?z=2&utm_source=x&b=1&FBCLID=x&gclid=x&mc_cid=x&mc_eid=x&spm=x&from=x&share_source=x&UTM_MEDIUM=x#part", "https://example.com/a?z=2&b=1")]
    [InlineData("https://www.example.com/a?x=a%20b&x=c+d", "https://www.example.com/a?x=a%20b&x=c+d")]
    [InlineData("https://example.com/Path/?utm_source=x", "https://example.com/Path")]
    [InlineData("https://example.com/path??x=1", "https://example.com/path??x=1")]
    [InlineData("https://example.com/path?a=1&&b=2", "https://example.com/path?a=1&&b=2")]
    [InlineData("not a URL", "not a URL")]
    [InlineData("/relative/path", "/relative/path")]
    public void Normalize_UsesSpecifiedCanonicalRules(string input, string expected)
    {
        UrlNormalizer.Normalize(input).Should().Be(expected);
        UrlNormalizer.Normalize(expected).Should().Be(expected);
    }
    [Fact]
    public void Normalize_MaximumLengthImplicitRootQuery_KeepsCanonicalRootAndMatchesExplicitRoot()
    {
        const string prefix = "https://example.com?x=";
        var url = prefix + new string('a', 2000 - prefix.Length);
        Uri.IsWellFormedUriString(url, UriKind.Absolute).Should().BeTrue();

        var normalized = UrlNormalizer.Normalize(url);

        normalized.Should().Be(url.Replace(".com?", ".com/?"));
        normalized.Length.Should().Be(2001);
        new Uri(normalized).AbsolutePath.Should().Be("/");
        UrlNormalizer.Normalize(url.Replace(".com?", ".com/?")).Should().Be(normalized);
        UrlNormalizer.Normalize(normalized).Should().Be(normalized);
    }

    [Fact]
    public void Normalize_MaximumLengthAuthority_KeepsCanonicalRootWithoutTruncation()
    {
        var url = "https://" + new string('a', 1980) + "@example.com";
        url.Length.Should().Be(2000);
        Uri.IsWellFormedUriString(url, UriKind.Absolute).Should().BeTrue();

        var normalized = UrlNormalizer.Normalize(url);

        normalized.Should().Be(url + "/");
        normalized.Length.Should().Be(2001);
        UrlNormalizer.Normalize(normalized).Should().Be(normalized);
    }

    [Theory]
    [InlineData('中')]
    [InlineData('\uE000')]
    [InlineData('\u0085')]
    public void Normalize_MaximumLengthUnicode_RemainsLosslessAndIdempotent(char character)
    {
        const string prefix = "https://example.com/";
        var url = prefix + new string(character, 2000 - prefix.Length);
        Uri.IsWellFormedUriString(url, UriKind.Absolute).Should().BeTrue();

        var normalized = UrlNormalizer.Normalize(url);

        normalized.Should().Be(new Uri(url).AbsoluteUri);
        Uri.UnescapeDataString(normalized).Should().Be(url);
        UrlNormalizer.Normalize(normalized).Should().Be(normalized);
    }

    [Theory]
    [InlineData("中文", "%E4%B8%AD%E6%96%87")]
    [InlineData("\uE000", "%EE%80%80")]
    [InlineData("\u0085", "%C2%85")]
    public void Normalize_EscapedAndLiteralUnicode_UseSameCanonicalValue(string literal, string escaped)
    {
        var plain = "https://example.com/" + literal;
        UrlNormalizer.Normalize(plain).Should().Be("https://example.com/" + escaped);
        UrlNormalizer.Normalize("https://example.com/" + escaped).Should().Be("https://example.com/" + escaped);
        UrlNormalizer.Normalize("https://example.com/path?q=" + escaped)
            .Should().Be("https://example.com/path?q=" + escaped);
    }

    [Theory]
    [InlineData("%2F", "/")]
    [InlineData("%3F", "?")]
    [InlineData("%23", "#")]
    [InlineData("%26", "&")]
    [InlineData("%3D", "=")]
    [InlineData("%2B", "+")]
    [InlineData("%252F", "%2F")]
    public void Normalize_EncodedDelimiters_DoNotCollideWithLiteralOrDoubleEncodedValues(string encoded, string literal)
    {
        var url = "https://example.com/a" + encoded + "b?x=a" + encoded + "b";
        var normalized = UrlNormalizer.Normalize(url);
        normalized.Should().Be(url);
        normalized.Should().NotBe(UrlNormalizer.Normalize("https://example.com/a" + literal + "b?x=a" + literal + "b"));
        UrlNormalizer.Normalize(normalized).Should().Be(normalized);
    }
}
