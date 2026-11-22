using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;
using MiniCc.Api.Core.ImageNs.Domain;

namespace MiniCc.Api.Core.ImageNs.Application;

public sealed class LegacyImageUrls(IOptions<ImageProxyOptions> proxy)
{
    private readonly Regex signed = new(
        $@"\A(?:{Regex.Escape(proxy.Value.ProxyUrl.TrimEnd('/'))}|/imageProxy)/[0-9]+x[0-9]+,s[A-Za-z0-9_-]+/(https?://.+)\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // Stop at the first signature marker so a proxied source URL keeps its own wrapper.
    private static readonly Regex HistoricalAbsolute = new(
        @"\Ahttps?://[^/?#\s]+[^?#\r\n]*?/[0-9]+x[0-9]+,s[A-Za-z0-9_-]{43}/(https?://.+)\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private string? FindOriginalUrl(string url)
    {
        var match = signed.Match(url);
        if (!match.Success) match = HistoricalAbsolute.Match(url);
        if (!match.Success) return null;

        var original = match.Groups[1].Value;
        return ImageHtml.TryGetHttpUri(original, out _) ? original : null;
    }

    public bool IsSignedUrl(string url) => FindOriginalUrl(url) is not null;

    public string StripUrl(string url) => FindOriginalUrl(url) ?? url;

    public string StripHtml(string html) => ImageHtml.Rewrite(html, (url, _, _) => StripUrl(url));

    public void Backfill(Article article, DateTimeOffset now)
    {
        if (article.Images != null) return;

        var body = StripHtml(article.ReadableContent);
        var cover = article.SavedFromExtensionAt.HasValue ? StripUrl(article.ImageUrl) : article.ImageUrl;
        article.NormalizeLegacyImages(body, ImageHtml.NormalizeUrl(cover), now);
    }
}
