using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Infra;

namespace MiniCc.Api.Core.ImageNs.Application;

public sealed class ImageUrlResolver(IOptions<ImageOptions> options, ImageProxyService proxy, LegacyImageUrls legacy)
{
    private readonly ImageOptions settings = options.Value;

    public string ResolveHtml(string html, IReadOnlyDictionary<string, ArticleImage>? images)
    {
        if (html.Length == 0) return html;
        if (settings.Mode == ImageMode.ImageProxy)
            return images == null ? ImageHtml.Rewrite(html, PreserveLegacyProxyUrl) : proxy.RewriteImages(html);

        if (images == null)
            return ImageHtml.Rewrite(html, (url, _, _) => legacy.StripUrl(url),
                static (original, mapped) => ImageHtml.TryGetHttpUri(mapped, out _));

        return ImageHtml.Rewrite(html, (url, _, _) => ResolveStoreUrl(url, images),
            (original, mapped) => original == mapped && ImageHtml.TryGetHttpUri(original, out _));
    }

    public string ResolveUrl(string url, IReadOnlyDictionary<string, ArticleImage>? images, bool savedFromExtension)
    {
        if (settings.Mode == ImageMode.ImageProxy)
            return images == null && savedFromExtension ? PreserveLegacyProxyUrl(url, 0, 0) : proxy.GetUrl(url);

        if (images != null) return ResolveStoreUrl(url, images);
        return savedFromExtension ? legacy.StripUrl(url) : url;
    }

    public string StoredUrl(string hash, string ext) => string.IsNullOrEmpty(settings.PublicBaseUrl)
        ? $"{settings.ApiUrl.TrimEnd('/')}/{hash}.{ext}"
        : $"{settings.PublicBaseUrl.TrimEnd('/')}/{ImageKeys.For(hash, ext)}";

    private string PreserveLegacyProxyUrl(string url, int width, int height) =>
        legacy.IsSignedUrl(url) ? url : proxy.GetUrl(url, width, height);

    private string ResolveStoreUrl(string url, IReadOnlyDictionary<string, ArticleImage> images)
    {
        return images.TryGetValue(ImageHtml.NormalizeUrl(url), out var image) &&
               image is { State: ArticleImageState.Done, Hash: { } hash, Ext: { } ext }
            ? StoredUrl(hash, ext)
            : url;
    }
}
