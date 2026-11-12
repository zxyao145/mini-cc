namespace MiniCc.Api.Core.ArticleNs.Domain.Services;

public static class UrlNormalizer
{
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "mc_cid", "mc_eid", "spm", "from", "share_source"
    };

    public static string Normalize(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            return url;

        var builder = new UriBuilder(uri) { Fragment = "", Host = uri.Host.ToLowerInvariant(), Scheme = uri.Scheme.ToLowerInvariant() };
        if (uri.IsDefaultPort) builder.Port = -1;
        if (builder.Path != "/") builder.Path = builder.Path.TrimEnd('/');
        var query = uri.Query.Length == 0 ? "" : uri.Query[1..];
        var normalizedQuery = string.Join("&", query.Split('&')
            .Where(parameter =>
            {
                var name = Uri.UnescapeDataString(parameter.Split('=', 2)[0]);
                return !name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) && !TrackingParameters.Contains(name);
            }));
        builder.Query = normalizedQuery.Length == 0 ? "" : "?" + normalizedQuery;
        return builder.Uri.AbsoluteUri;
    }
}
