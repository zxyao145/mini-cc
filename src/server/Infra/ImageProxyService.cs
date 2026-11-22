using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ImageNs.Domain;
using System.Security.Cryptography;
using System.Text;

namespace MiniCc.Api.Infra;

public class ImageProxyOptions
{
    public string ProxyUrl { get; set; } = "/imageProxy";
    public string SignatureKey { get; set; } = "";
}

public class ImageProxyService(IOptions<ImageProxyOptions> options)
{
    private readonly ImageProxyOptions _options = options.Value;

    public string GetUrl(string url, int width = 0, int height = 0)
    {
        var proxyUrl = _options.ProxyUrl.TrimEnd('/');
        if (url.StartsWith(proxyUrl + "/", StringComparison.Ordinal) ||
            !ImageHtml.TryGetHttpUri(url, out var uri))
        {
            return url;
        }

        url = uri.AbsoluteUri;
        var dimensions = $"{Math.Max(0, width)}x{Math.Max(0, height)}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.SignatureKey), Encoding.UTF8.GetBytes($"{url}#{dimensions}"));
        var signature = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{proxyUrl}/{dimensions},s{signature}/{url}";
    }

    public string RewriteImages(string html) => ImageHtml.Rewrite(html, GetUrl);
}
