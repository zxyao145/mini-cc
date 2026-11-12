using Microsoft.Extensions.Options;
using MiniCc.Api.Core.FeedNs.Infrastructure;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Test.Feeds;

public class FeedParserTests
{
    private readonly FeedParser parser = new(new ImageProxyService(Options.Create(new ImageProxyOptions { SignatureKey = "test", ProxyUrl = "/img" })));

    [Fact]
    public void Rss_PrefersEncodedContentAndSanitizesRelativeImages()
    {
        var xml = "<rss version='2.0' xmlns:content='http://purl.org/rss/1.0/modules/content/'><channel><title>Source</title><link>https://example.com</link><description>feed</description><ttl>90</ttl><item><title>Post</title><guid isPermaLink='false'>opaque</guid><link>https://example.com/posts/1</link><description>short</description><content:encoded><![CDATA[<script>alert(1)</script><pre class='language-csharp'>" + new string('a', 501) + "</pre><img src='../photo.jpg' onerror='alert(1)'>]]></content:encoded><pubDate>invalid</pubDate></item><item><guid isPermaLink='false'>no-url</guid></item></channel></rss>";
        var feed = parser.Parse(xml, "https://example.com/feed");
        feed.MinIntervalMinutes.Should().Be(90); feed.Skipped.Should().Be(1);
        var item = feed.Items.Single(); item.EntryKey.Should().Be("opaque"); item.ContentKind.Should().Be(ContentKind.Full);
        item.ReadableContent.Should().NotContain("<script").And.NotContain("onerror").And.Contain("language-csharp").And.Contain("/img/");
        item.Summary.Should().Be("short"); item.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void Atom_UsesAlternateLinkAndSummaryAndKeepsOpaqueId()
    {
        var feed = parser.Parse("<feed xmlns='http://www.w3.org/2005/Atom'><title>Atom</title><id>urn:feed</id><updated>2026-01-01T00:00:00Z</updated><entry><id>urn:item</id><title>Post</title><link rel='self' href='https://example.com/self'/><link rel='alternate' href='https://example.com/article'/><summary type='html'>&lt;p&gt;brief…&lt;/p&gt;</summary><updated>2026-01-02T00:00:00Z</updated></entry></feed>", "https://example.com/feed");
        feed.Format.ToString().Should().Be("Atom"); feed.Items.Single().EntryKey.Should().Be("urn:item");
        feed.Items.Single().Url.Should().Be("https://example.com/article"); feed.Items.Single().ContentKind.Should().Be(ContentKind.SummaryOnly);
        feed.Items.Single().PublishedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("…")][InlineData("...")][InlineData("Read more")][InlineData("阅读全文")][InlineData("[…]")]
    public void LongTruncatedContent_IsStillSummary(string ending) => FeedParser.IsSummary(new string('a', 501) + ending).Should().BeTrue();

    [Fact]
    public void Rdf_IsUnsupported() => ((Action)(() => parser.Parse("<rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'/>", "https://example.com"))).Should().Throw<FeedException>().WithMessage("不支持的订阅格式");

    [Fact]
    public void EmojiAtSummaryBoundary_RemainsValidUtf16()
    {
        var body = "a" + string.Concat(Enumerable.Repeat("😀", 200));
        var xml = "<rss version='2.0'><channel><title>Source</title><link>https://example.com</link><description>D</description><item><title>Post</title><link>https://example.com/post</link><description>" + body + "</description></item></channel></rss>";
        var summary = parser.Parse(xml, "https://example.com/feed").Items.Single().Summary;
        ((Action)(() => new System.Text.UTF8Encoding(false, true).GetBytes(summary))).Should().NotThrow();
        summary.Length.Should().BeLessThanOrEqualTo(300);
    }

    [Fact]
    public void OpaqueGuidWithoutLink_IsSkippedAndDoesNotBecomeRelativeUrl()
    {
        var xml = "<rss version='2.0'><channel><title>Source</title><link>https://example.com</link><description>D</description><item><title>Post</title><guid>opaque-identity</guid></item></channel></rss>";
        parser.Parse(xml, "https://example.com/feed").Items.Should().BeEmpty();
    }

    private string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "feeds", name + ".xml"));
    [Fact]
    public void RssFixture_UsesLinkFallbackRelativeImagesCoverAndSyndicationInterval()
    {
        var parsed = parser.Parse(Fixture("rss"), "https://example.com/feed");
        parsed.MinIntervalMinutes.Should().Be(360); parsed.Skipped.Should().Be(1);
        parsed.Items[0].EntryKey.Should().Be("https://example.com/post");
        parsed.Items[0].ImageUrl.Should().Be("https://example.com/cover.jpg");
        parsed.Items[0].ReadableContent.Should().Contain("https://example.com/image.png").And.Contain("https://example.com/read");
        parsed.Items[1].Url.Should().Be("https://example.com/permalink");
    }
    [Fact]
    public void AtomFixture_PreservesXhtmlAndAuthor()
    {
        var entry = parser.Parse(Fixture("atom"), "https://example.com/feed").Items.Single();
        entry.Author.Should().Be("Writer"); entry.ReadableContent.Should().Contain("<p>XHTML body</p>").And.Contain("https://example.com/image.png");
    }
    [Fact]
    public void ExternalDtdEntity_IsNeverExpanded() => ((Action)(() => parser.Parse(Fixture("dtd"), "https://example.com/feed"))).Should().Throw<System.Xml.XmlException>();

    [Fact]
    public void AtomTextFixture_PreservesExplicitAndDefaultLiteralContentAndSummary()
    {
        var items = parser.Parse(Fixture("atom-text"), "https://example.com/feed").Items;
        const string literal = "Literal <strong>code</strong> and <script>sample</script>.";
        const string summary = "Summary <b>literal</b> & words";
        foreach (var item in items.Take(2))
        {
            item.OriginalContent.Should().Be(literal);
            FeedParser.PlainText(item.ReadableContent).Should().Be(literal);
            item.ReadableContent.Should().NotContain("<strong>").And.NotContain("<script>");
            item.Length.Should().Be(literal.Length);
        }
        items[0].Summary.Should().Be(summary); items[1].Summary.Should().Be(literal);
        FeedParser.PlainText(items[2].ReadableContent).Should().Be(summary); items[2].Summary.Should().Be(summary);
        items[3].ReadableContent.Should().Contain("<strong>code</strong>"); items[3].Summary.Should().Be("Formatted summary");
    }

    [Fact]
    public void AtomBasesFixture_ResolvesInheritedFeedEntryLinkContentAndNestedXhtmlBases()
    {
        var feed = parser.Parse(Fixture("atom-bases"), "https://transport.example.net/feeds/atom.xml");
        feed.SiteUrl.Should().Be("https://example.org/blog/index.html");
        feed.Items[0].Url.Should().Be("https://example.org/blog/posts/one");
        feed.Items[0].ImageUrl.Should().Be("https://cdn.example.org/assets/cover.jpg");
        feed.Items[0].ReadableContent.Should().Contain("https://cdn.example.org/assets/nested/article.html")
            .And.Contain("https://cdn.example.org/assets/pictures/photo.jpg")
            .And.Contain("https://cdn.example.org/assets/nested/inherited.html");
        feed.Items[1].Url.Should().Be("https://example.org/blog/other/two");
        feed.Items[1].EntryKey.Should().Be("https://example.org/blog/other/two");
        feed.Items[1].ImageUrl.Should().Be("https://media.example.org/summary/preview.png");
    }

    [Fact]
    public void RssBases_ApplyToEntryLinkMediaAndEncodedBody_WithoutChangingNoBaseFallback()
    {
        var xml = "<rss version='2.0' xml:base='https://example.org/root/' xmlns:media='http://search.yahoo.com/mrss/' xmlns:content='http://purl.org/rss/1.0/modules/content/'><channel><title>R</title><link xml:base='../'>site</link><description>D</description><item xml:base='posts/'><title>One</title><link>one</link><media:thumbnail xml:base='../../covers/' url='one.png'/><content:encoded xml:base='https://cdn.example.org/code/'><![CDATA[<img src='image.png'>text]]></content:encoded></item></channel></rss>";
        var feed = parser.Parse(xml, "https://transport.example.net/rss");
        feed.SiteUrl.Should().Be("https://example.org/site");
        feed.Items[0].Url.Should().Be("https://example.org/root/posts/one");
        feed.Items[0].ImageUrl.Should().Be("https://example.org/covers/one.png");
        feed.Items[0].ReadableContent.Should().Contain("https://cdn.example.org/code/image.png");
        var noBase = parser.Parse("<rss version='2.0'><channel><title>R</title><link>https://example.org</link><description>D</description><item><title>One</title><link>https://example.org/posts/one</link><description><![CDATA[<img src='image.png'>]]></description></item></channel></rss>", "https://transport.example.net/rss");
        noBase.Items[0].ImageUrl.Should().Be("https://example.org/posts/image.png");
    }
}
