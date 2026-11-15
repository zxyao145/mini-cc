using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;

public class Content : ValueObject
{
    public string OriginalContent { get; private set; }
    public string ReadableContent { get; private set; }
    public int Length { get; private set; }

    private Content(string originalContent, string readableContent, int length)
    {
        OriginalContent = originalContent;
        ReadableContent = readableContent;
        Length = length;
    }

    public static Content Create(string originalContent, string readableContent)
    {
        var length = readableContent?.Length ?? 0;

        return Create(originalContent, readableContent ?? "", length);
    }

    public static Content Create(string originalContent, string readableContent, int length)
    {
        if (string.IsNullOrEmpty(originalContent))
            throw new ArgumentException("Original content cannot be null or empty", nameof(originalContent));

        var cleanReadableContent = readableContent ?? string.Empty;

        return new Content(originalContent, cleanReadableContent, length);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return OriginalContent;
        yield return ReadableContent;
        yield return Length;
    }
}