using System.Diagnostics.CodeAnalysis;
using System.Net;
using HtmlAgilityPack;

namespace MiniCc.Api.Core.ImageNs.Domain;

public delegate string ImageUrlMap(string url, int width, int height);

public static class ImageHtml
{
    public static string Rewrite(string html, ImageUrlMap map, Func<string, string, bool>? needsNoReferrer = null)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        foreach (var node in document.DocumentNode.Descendants().Where(n => n.Name is "img" or "source"))
        {
            var noReferrer = false;
            if (node.Attributes["src"] is { } src)
            {
                int.TryParse(node.GetAttributeValue("width", ""), out var width);
                int.TryParse(node.GetAttributeValue("height", ""), out var height);
                src.Value = WebUtility.HtmlEncode(Map(Decode(src), width, height));
            }

            if (node.Attributes["srcset"] is { } srcset)
            {
                var candidates = SrcsetCandidates(Decode(srcset)).Select(candidate =>
                    Map(candidate.Url, 0, 0) + (candidate.Descriptor.Length == 0 ? "" : " " + candidate.Descriptor));
                srcset.Value = WebUtility.HtmlEncode(string.Join(", ", candidates));
            }

            node.Attributes.Remove("crossorigin");
            if (noReferrer)
            {
                var image = node.Name == "img" ? node : node.ParentNode?.Name == "picture"
                    ? node.ParentNode.Descendants("img").FirstOrDefault()
                    : null;
                image?.SetAttributeValue("referrerpolicy", "no-referrer");
            }

            string Map(string original, int width, int height)
            {
                var mapped = map(original, width, height);
                noReferrer |= needsNoReferrer?.Invoke(original, mapped) == true;
                return mapped;
            }
        }
        return document.DocumentNode.OuterHtml;
    }

    public static string NormalizeUrl(string url) => TryGetHttpUri(url, out var uri) ? uri.AbsoluteUri : url;

    internal static bool TryGetHttpUri(string url, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme is "http" or "https";

    public static string Normalize(string html) => Rewrite(html, (url, _, _) => NormalizeUrl(url));

    public static IReadOnlyList<string> CollectDownloadUrls(string html, string coverUrl)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in document.DocumentNode.Descendants()
                     .Where(n => n.Name == "img" || (n.Name == "source" && n.ParentNode?.Name == "picture")))
        {
            if (node.Attributes["src"] is { } src)
                Add(Decode(src));
            if (node.Attributes["srcset"] is { } srcset)
                foreach (var candidate in SrcsetCandidates(Decode(srcset)))
                    Add(candidate.Url);
        }

        Add(coverUrl);
        return urls;

        void Add(string url)
        {
            if (TryGetHttpUri(url, out var uri) && seen.Add(uri.AbsoluteUri))
                urls.Add(uri.AbsoluteUri);
        }
    }

    private static string Decode(HtmlAttribute attribute) => HtmlEntity.DeEntitize(attribute.Value) ?? "";

    private static IEnumerable<(string Url, string Descriptor)> SrcsetCandidates(string srcset)
    {
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

            yield return (url, descriptor);
        }
    }
}
