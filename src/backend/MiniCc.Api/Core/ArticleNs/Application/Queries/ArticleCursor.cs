using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public record ArticleCursor(
    [property: JsonPropertyName("s")] string Sort,
    [property: JsonPropertyName("v")] DateTimeOffset? Value,
    [property: JsonPropertyName("id")] Guid Id)
{
    public static string Encode(string sort, DateTimeOffset? value, Guid id) =>
        WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new ArticleCursor(sort, value, id)));

    public static ArticleCursor Decode(string encoded, string sort)
    {
        try
        {
            if (encoded.Length > 512) throw new ArgumentException();
            var cursor = JsonSerializer.Deserialize<ArticleCursor>(WebEncoders.Base64UrlDecode(encoded));
            if (cursor == null || cursor.Sort != sort || cursor.Id == Guid.Empty ||
                (cursor.Value == null && sort is not ("read_desc" or "published_desc"))) throw new ArgumentException();
            return cursor;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            throw new ArgumentException("Invalid cursor or cursor sort does not match this request.");
        }
    }
}
