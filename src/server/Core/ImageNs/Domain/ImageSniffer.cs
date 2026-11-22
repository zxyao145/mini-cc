using System.Text;
using System.Xml;

namespace MiniCc.Api.Core.ImageNs.Domain;

public static class ImageSniffer
{
    private const int SvgPrefixLimit = 4 * 1024;

    public static string? Detect(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(new byte[] { 0xff, 0xd8, 0xff })) return "jpg";
        if (content.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a })) return "png";
        if (content.StartsWith("GIF87a"u8) || content.StartsWith("GIF89a"u8)) return "gif";
        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8)) return "webp";
        if (content.Length >= 12 && content[4..8].SequenceEqual("ftyp"u8)
            && (content[8..12].SequenceEqual("avif"u8) || content[8..12].SequenceEqual("avis"u8))) return "avif";

        var prefix = content[..Math.Min(content.Length, SvgPrefixLimit)];
        if (prefix.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) prefix = prefix[3..];
        var text = Encoding.UTF8.GetString(prefix).TrimStart();
        if (text.StartsWith("<svg", StringComparison.Ordinal)) return "svg";
        if (text.StartsWith("<?xml", StringComparison.Ordinal) && HasSvgElement(text)) return "svg";
        return null;
    }

    private static bool HasSvgElement(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null
            });

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "svg")
                    return true;
            }
        }
        catch (XmlException)
        {
            return false;
        }

        return false;
    }
}
