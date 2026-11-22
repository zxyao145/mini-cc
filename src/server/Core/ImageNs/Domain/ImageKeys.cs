namespace MiniCc.Api.Core.ImageNs.Domain;

public static class ImageKeys
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        ["jpg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["webp"] = "image/webp",
        ["avif"] = "image/avif",
        ["svg"] = "image/svg+xml"
    };

    public static string For(string hash, string ext) => $"{hash[..2]}/{hash}.{ext}";

    public static bool TryParseKey(string key, out string hash, out string ext)
    {
        if (key.Length > 3 && key[2] == '/' && TryParseName(key[3..], out hash, out ext) &&
            key.AsSpan(0, 2).SequenceEqual(hash.AsSpan(0, 2)))
        {
            return true;
        }

        hash = ext = "";
        return false;
    }

    public static bool TryParseName(string name, out string hash, out string ext)
    {
        hash = ext = "";
        if (name.Length < 68 || name[64] != '.' ||
            name.AsSpan(0, 64).ContainsAnyExcept("0123456789abcdef"))
        {
            return false;
        }

        var extension = name[65..];
        if (!ContentTypes.ContainsKey(extension))
            return false;

        hash = name[..64];
        ext = extension;
        return true;
    }

    public static string ContentType(string ext) => ContentTypes.TryGetValue(ext, out var contentType)
        ? contentType
        : throw new ArgumentOutOfRangeException(nameof(ext));
}
