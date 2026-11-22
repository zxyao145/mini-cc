using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Infra;
using System.Net;

namespace MiniCc.Api.Test.Images;

internal static class TestImages
{
    public const string SignatureKey = "test-signing-key";

    public static ImageProxyService Proxy(string key = SignatureKey, string proxyUrl = "/imageProxy") =>
        new(Options.Create(new ImageProxyOptions { SignatureKey = key, ProxyUrl = proxyUrl }));

    public static LegacyImageUrls Legacy(string proxyUrl = "/imageProxy") =>
        new(Options.Create(new ImageProxyOptions { ProxyUrl = proxyUrl }));

    public static ImageUrlResolver Resolver(Action<ImageOptions>? configure = null)
    {
        var options = new ImageOptions();
        configure?.Invoke(options);
        return new(Options.Create(options), Proxy(), Legacy());
    }

    // The traversal before M5, kept as the compatibility reference.
    public static string LegacyRewrite(string html)
    {
        var proxy = Proxy();
        var document = new HtmlDocument();
        document.LoadHtml(html);
        foreach (var node in document.DocumentNode.Descendants().Where(n => n.Name is "img" or "source"))
        {
            if (node.Attributes["src"] is { } src)
            {
                int.TryParse(node.GetAttributeValue("width", ""), out var width);
                int.TryParse(node.GetAttributeValue("height", ""), out var height);
                src.Value = WebUtility.HtmlEncode(proxy.GetUrl(HtmlEntity.DeEntitize(src.Value) ?? "", width, height));
            }

            if (node.Attributes["srcset"] is { } srcset)
            {
                srcset.Value = WebUtility.HtmlEncode(RewriteSrcset(HtmlEntity.DeEntitize(srcset.Value) ?? "", proxy));
            }
            node.Attributes.Remove("crossorigin");
        }
        return document.DocumentNode.OuterHtml;
    }

    private static string RewriteSrcset(string srcset, ImageProxyService proxy)
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

            candidates.Add(proxy.GetUrl(url) + (descriptor.Length == 0 ? "" : " " + descriptor));
        }
        return string.Join(", ", candidates);
    }
}
