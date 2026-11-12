using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ganss.Xss;
using HtmlAgilityPack;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.FeedNs.Domain;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Core.FeedNs.Infrastructure;

public record ParsedEntry(string EntryKey, string Url, string Title, string Author, string OriginalContent,
    string ReadableContent, string Summary, string ImageUrl, int Length, ContentKind ContentKind, DateTimeOffset? PublishedAt)
{
    public string ContentHash => FeedIdentity.Hash(OriginalContent);
}
public record ParsedFeed(string Title, string? SiteUrl, FeedFormat Format, int MinIntervalMinutes, List<ParsedEntry> Items, int Skipped);
public class FeedException(string message) : Exception(message);

public class FeedParser(ImageProxyService images)
{
    public ParsedFeed Parse(string xml, string feedUrl)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        var document = XDocument.Load(reader);
        var root = document.Root ?? throw new FeedException("不是订阅地址");
        var format = root.Name.LocalName switch { "rss" => FeedFormat.Rss, "feed" => FeedFormat.Atom, _ => throw new FeedException("不支持的订阅格式") };
        // Syndication is strict about malformed dates. Keep valid values and treat malformed ones as absent.
        foreach (var date in root.Descendants().Where(x => x.Name.LocalName is "pubDate" or "lastBuildDate" or "published" or "updated").ToList())
            if (!DateTimeOffset.TryParse(date.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _)) date.Remove();
        using var cleanReader = XmlReader.Create(new StringReader(document.ToString(SaveOptions.DisableFormatting)),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
        var feed = SyndicationFeed.Load(cleanReader) ?? throw new FeedException("不是订阅地址");
        var container = format == FeedFormat.Rss ? root.Elements().First(x => x.Name.LocalName == "channel") : root;
        var rawItems = container.Elements().Where(x => x.Name.LocalName == (format == FeedFormat.Rss ? "item" : "entry")).ToArray();
        var items = new List<ParsedEntry>(); var skipped = 0; var index = 0;
        foreach (var item in feed.Items)
        {
            var raw = rawItems[index++];
            var guid = raw.Elements().FirstOrDefault(x => x.Name.LocalName == (format == FeedFormat.Rss ? "guid" : "id"));
            var linkElement = raw.Elements().FirstOrDefault(x => x.Name.LocalName == "link" &&
                (format == FeedFormat.Rss || x.Attribute("rel")?.Value is null or "" or "alternate"));
            var link = format == FeedFormat.Rss ? linkElement?.Value : linkElement?.Attribute("href")?.Value;
            if (string.IsNullOrWhiteSpace(link) && format == FeedFormat.Rss && guid?.Attribute("isPermaLink")?.Value != "false"
                && Uri.TryCreate(guid?.Value, UriKind.Absolute, out var permalink) && permalink.Scheme is "http" or "https") link = permalink.AbsoluteUri;
            var url = Absolute(link, XmlBase(linkElement ?? raw, feedUrl));
            if (url == null) { skipped++; continue; }
            var published = Date(item.PublishDate) ?? Date(item.LastUpdatedTime);
            var key = !string.IsNullOrWhiteSpace(guid?.Value) ? guid.Value : url;
            var encoded = item.ElementExtensions.FirstOrDefault(e => e.OuterName == "encoded" && e.OuterNamespace == "http://purl.org/rss/1.0/modules/content/")?.GetObject<string>();
            var selectedText = item.Content as TextSyndicationContent ?? item.Summary;
            var body = encoded ?? selectedText?.Text ?? "";
            var literalBody = format == FeedFormat.Atom && encoded == null && selectedText?.Type == "text";
            if (item.Content is XmlSyndicationContent xmlContent)
            {
                using var contentReader = xmlContent.GetReaderAtContent();
                var element = XElement.Load(contentReader);
                body = string.Concat(element.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)));
                literalBody = false;
            }
            var bodyElement = encoded != null
                ? raw.Elements().FirstOrDefault(x => x.Name.LocalName == "encoded" && x.Name.NamespaceName == "http://purl.org/rss/1.0/modules/content/")
                : raw.Elements().FirstOrDefault(x => x.Name.LocalName == (item.Content != null ? "content" : format == FeedFormat.Rss ? "description" : "summary"));
            var sanitized = Sanitize(literalBody ? WebUtility.HtmlEncode(body) : body, XmlBase(bodyElement ?? raw, feedUrl, url));
            var text = PlainText(sanitized);
            var summary = item.Summary == null ? text : format == FeedFormat.Atom && item.Summary.Type == "text"
                ? item.Summary.Text.Trim() : PlainText(item.Summary.Text);
            var media = raw.Descendants().FirstOrDefault(x => x.Name.NamespaceName == "http://search.yahoo.com/mrss/" && x.Name.LocalName is "thumbnail" or "content");
            var enclosure = raw.Elements().FirstOrDefault(x => (x.Name.LocalName == "enclosure" || x.Name.LocalName == "link" && x.Attribute("rel")?.Value == "enclosure") && x.Attribute("type")?.Value.StartsWith("image/") == true);
            var cover = Absolute(media?.Attribute("url")?.Value, XmlBase(media ?? raw, feedUrl, url))
                ?? Absolute(enclosure?.Attribute(format == FeedFormat.Rss ? "url" : "href")?.Value, XmlBase(enclosure ?? raw, feedUrl, url));
            if (cover == null) { var html = new HtmlDocument(); html.LoadHtml(sanitized); cover = html.DocumentNode.Descendants("img").FirstOrDefault()?.GetAttributeValue("src", ""); }
            items.Add(new(key, url, string.IsNullOrWhiteSpace(item.Title?.Text) ? "无标题" : item.Title.Text,
                item.Authors.FirstOrDefault()?.Name ?? feed.Title?.Text ?? "", body,
                images.RewriteImages(sanitized), FeedText.Limit(summary, 300), cover ?? "", text.Length,
                IsSummary(text) ? ContentKind.SummaryOnly : ContentKind.Full, published));
        }
        var minutes = 30;
        if (int.TryParse(root.Descendants().FirstOrDefault(x => x.Name.LocalName == "ttl")?.Value, out var ttl)) minutes = Math.Max(minutes, ttl);
        var period = root.Descendants().FirstOrDefault(x => x.Name.NamespaceName == "http://purl.org/rss/1.0/modules/syndication/" && x.Name.LocalName == "updatePeriod")?.Value;
        var periodMinutes = period switch { "hourly" => 60, "daily" => 1440, "weekly" => 10080, "monthly" => 43200, "yearly" => 525600, _ => 0 };
        var frequency = int.TryParse(root.Descendants().FirstOrDefault(x => x.Name.LocalName == "updateFrequency")?.Value, out var count) && count > 0 ? count : 1;
        minutes = Math.Max(minutes, periodMinutes / frequency);
        var siteLink = container.Elements().FirstOrDefault(x => x.Name.LocalName == "link" &&
            (format == FeedFormat.Rss || x.Attribute("rel")?.Value is null or "" or "alternate"));
        var siteUrl = Absolute(format == FeedFormat.Rss ? siteLink?.Value : siteLink?.Attribute("href")?.Value, XmlBase(siteLink ?? container, feedUrl));
        return new(feed.Title?.Text ?? "未命名订阅", siteUrl, format, minutes, items, skipped);
    }

    private static string XmlBase(XElement element, string documentUrl, string? noBaseFallback = null)
    {
        var bases = element.AncestorsAndSelf().Reverse().Select(e => e.Attribute(XNamespace.Xml + "base")?.Value)
            .Where(value => !string.IsNullOrEmpty(value)).ToList();
        if (bases.Count == 0) return noBaseFallback ?? documentUrl;
        var basis = documentUrl;
        foreach (var value in bases)
            if (Uri.TryCreate(new Uri(basis), value, out var resolved)) basis = resolved.AbsoluteUri;
        return basis;
    }

    public static bool IsSummary(string text) => text.Trim().Length < 500 || new[] { "…", "...", "Read more", "阅读全文", "[…]" }.Any(x => text.TrimEnd().EndsWith(x, StringComparison.OrdinalIgnoreCase));
    public static string PlainText(string html) { var doc = new HtmlDocument(); doc.LoadHtml(html); return (HtmlEntity.DeEntitize(doc.DocumentNode.InnerText) ?? "").Trim(); }
    public static string? Absolute(string? value, string baseUrl) => !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(new Uri(baseUrl), value, out var uri) && uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
    private static DateTimeOffset? Date(DateTimeOffset date) => date == DateTimeOffset.MinValue ? null : date.ToUniversalTime();
    private static string Sanitize(string html, string url)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.UnionWith(["figure", "figcaption", "picture", "source"]);
        sanitizer.RemovingAttribute += (_, e) => { if (e.Attribute.Name == "class" && e.Tag.TagName is "PRE" or "CODE") e.Cancel = true; };
        // Resolve nested XHTML/HTML xml:base before the sanitizer removes those attributes.
        var document = new HtmlDocument(); document.LoadHtml(html);
        foreach (var node in document.DocumentNode.Descendants())
        {
            var basis = url;
            foreach (var ancestor in node.AncestorsAndSelf().Reverse())
            {
                var value = HtmlEntity.DeEntitize(ancestor.GetAttributeValue("xml:base", ""));
                if (!string.IsNullOrEmpty(value) && Uri.TryCreate(new Uri(basis), value, out var resolved)) basis = resolved.AbsoluteUri;
            }
            foreach (var attribute in node.Attributes.Where(a => sanitizer.UriAttributes.Contains(a.Name)).ToList())
                if (Uri.TryCreate(new Uri(basis), HtmlEntity.DeEntitize(attribute.Value), out var resolved)) attribute.Value = WebUtility.HtmlEncode(resolved.AbsoluteUri);
        }
        return sanitizer.Sanitize(document.DocumentNode.OuterHtml, url);
    }
}
