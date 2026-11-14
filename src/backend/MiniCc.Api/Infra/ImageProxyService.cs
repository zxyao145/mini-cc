using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using System.Net;
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
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return url;
        }

        url = uri.AbsoluteUri;
        var dimensions = $"{Math.Max(0, width)}x{Math.Max(0, height)}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_options.SignatureKey), Encoding.UTF8.GetBytes($"{url}#{dimensions}"));
        var signature = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{proxyUrl}/{dimensions},s{signature}/{url}";
    }

    public string RewriteImages(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        foreach (var node in document.DocumentNode.Descendants().Where(n => n.Name is "img" or "source"))
        {
            if (node.Attributes["src"] is { } src)
            {
                int.TryParse(node.GetAttributeValue("width", ""), out var width);
                int.TryParse(node.GetAttributeValue("height", ""), out var height);
                src.Value = WebUtility.HtmlEncode(GetUrl(HtmlEntity.DeEntitize(src.Value) ?? "", width, height));
            }

            if (node.Attributes["srcset"] is { } srcset)
            {
                srcset.Value = WebUtility.HtmlEncode(RewriteSrcset(HtmlEntity.DeEntitize(srcset.Value) ?? ""));
            }
            node.Attributes.Remove("crossorigin");
        }
        return document.DocumentNode.OuterHtml;
    }

    private string RewriteSrcset(string srcset)
    {
        var candidates = new List<string>();
        var position = 0;
        while (position < srcset.Length)
        {
            while (position < srcset.Length && (char.IsWhiteSpace(srcset[position]) || srcset[position] == ','))
                position++;
            if (position == srcset.Length)
                break;

            var start = position;
            while (position < srcset.Length && !char.IsWhiteSpace(srcset[position]))
                position++;
            var url = srcset[start..position];
            var descriptor = "";
            if (url.EndsWith(','))
            {
                url = url.TrimEnd(',');
            }
            else
            {
                start = position;
                while (position < srcset.Length && srcset[position] != ',')
                    position++;
                descriptor = srcset[start..position].Trim();
            }

            candidates.Add(GetUrl(url) + (descriptor.Length == 0 ? "" : " " + descriptor));
        }
        return string.Join(", ", candidates);
    }
}
