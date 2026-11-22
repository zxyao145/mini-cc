using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ArticleNs.Infrastructure.EntityConfigurations;

internal static class ArticleImagesJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter<ArticleImageState>(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static readonly ValueConverter<Dictionary<string, ArticleImage>?, string> Converter = new(
        images => JsonSerializer.Serialize(images, Options),
        json => JsonSerializer.Deserialize<Dictionary<string, ArticleImage>>(json, Options));

    public static readonly ValueComparer<Dictionary<string, ArticleImage>> Comparer = new(
        (left, right) => Equal(left, right),
        images => GetImagesHashCode(images),
        images => new Dictionary<string, ArticleImage>(images, StringComparer.Ordinal));

    private static bool Equal(Dictionary<string, ArticleImage>? left, Dictionary<string, ArticleImage>? right) =>
        ReferenceEquals(left, right) ||
        (left is not null && right is not null && left.Count == right.Count &&
         left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value));

    private static int GetImagesHashCode(Dictionary<string, ArticleImage> images)
    {
        var hash = images.Count;
        foreach (var pair in images)
            hash ^= HashCode.Combine(pair.Key, pair.Value);
        return hash;
    }
}
