using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;
using MiniCc.Api.Core.ArticleNs.Domain.Services;

namespace MiniCc.Api.Core.FeedNs.Infrastructure;

public record FeedFetch(string FeedUrl, int StatusCode, string? Body, string? ETag, string? LastModified, DateTimeOffset? RetryAfter);

public class FeedFetcher(IHttpClientFactory clients)
{
    public async Task<FeedFetch> FetchAsync(string url, string? etag, string? lastModified, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            for (var discovery = 0; discovery < 2; discovery++)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new FeedException("订阅地址必须是 http 或 https");
                url = UrlNormalizer.Normalize(url);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                request.Headers.TryAddWithoutValidation("If-Modified-Since", lastModified);
                request.Headers.TryAddWithoutValidation("User-Agent", "MiniCC/1.0 (+self-hosted feed reader)");
                using var client = clients.CreateClient("feeds");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                var finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                var retry = response.Headers.RetryAfter;
                var result = new FeedFetch(finalUrl, (int)response.StatusCode, null, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified?.ToString("R"), retry?.Date ?? (retry?.Delta is { } delta ? DateTimeOffset.UtcNow + delta : null));
                if (!response.IsSuccessStatusCode) return result;
                const int limit = 10 * 1024 * 1024;
                if (response.Content.Headers.ContentLength > limit) throw new FeedException("订阅响应超过 10MB");
                await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
                using var buffer = new MemoryStream(); var chunk = new byte[8192];
                while (true)
                {
                    var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, limit + 1 - (int)buffer.Length)), deadline.Token);
                    if (read == 0) break;
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > limit) throw new FeedException("订阅响应超过 10MB");
                }
                var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
                var bytes = buffer.ToArray();
                var html = response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true;
                string body;
                if (string.IsNullOrWhiteSpace(charset) && !HasBom(bytes))
                {
                    // XmlReader consumes the original bytes so a declaration such as Latin-1 is honored.
                    try
                    {
                        using var xml = XmlReader.Create(new MemoryStream(bytes), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
                        body = XDocument.Load(xml).ToString(SaveOptions.DisableFormatting);
                    }
                    catch (XmlException)
                    {
                        body = await DecodeTextAsync(bytes, null, deadline.Token);
                        if (!html && !LooksLikeHtml(body)) throw;
                        html = true;
                    }
                }
                else
                {
                    // BOM is authoritative; otherwise the explicit HTTP charset takes precedence over XML.
                    body = await DecodeTextAsync(bytes, charset, deadline.Token);
                    if (!html)
                    {
                        try { using var xml = XmlReader.Create(new StringReader(body), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }); while (xml.Read()) { } }
                        catch (XmlException) { html = LooksLikeHtml(body); }
                    }
                }
                deadline.Token.ThrowIfCancellationRequested();
                if (!html) return result with { Body = body };
                if (discovery != 0) throw new FeedException("未找到订阅地址");
                var document = new HtmlDocument(); document.LoadHtml(body);
                var alternate = document.DocumentNode.Descendants("link").FirstOrDefault(n => n.GetAttributeValue("rel", "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("alternate", StringComparer.OrdinalIgnoreCase)
                    && n.GetAttributeValue("type", "").ToLowerInvariant() is "application/rss+xml" or "application/atom+xml");
                url = FeedParser.Absolute(alternate?.GetAttributeValue("href", ""), finalUrl) ?? throw new FeedException("未找到订阅地址");
            }
            throw new FeedException("未找到订阅地址");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new FeedException("订阅请求超时（20 秒）"); }
        catch (HttpRequestException ex) { throw new FeedException("无法访问订阅源：" + ex.Message); }
    }

    private static bool LooksLikeHtml(string body) => body.Contains("<html", StringComparison.OrdinalIgnoreCase) || body.Contains("<link", StringComparison.OrdinalIgnoreCase);
    private static bool HasBom(ReadOnlySpan<byte> bytes) => bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })
        || bytes.StartsWith(new byte[] { 0xff, 0xfe }) || bytes.StartsWith(new byte[] { 0xfe, 0xff })
        || bytes.StartsWith(new byte[] { 0x00, 0x00, 0xfe, 0xff });
    private static async Task<string> DecodeTextAsync(byte[] bytes, string? charset, CancellationToken ct)
    {
        var encoding = HasBom(bytes) || string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(ct);
    }
}
