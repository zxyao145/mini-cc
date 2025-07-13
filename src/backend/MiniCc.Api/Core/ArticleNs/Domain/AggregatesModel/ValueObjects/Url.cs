using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;

public class Url : ValueObject
{
    public string Value { get; private set; }

    private Url(string value)
    {
        Value = value;
    }

    public static Url Create(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));

        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            throw new ArgumentException("Invalid URL format", nameof(url));

        return new Url(url);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator string(Url url) => url.Value;

    public override string ToString() => Value;
}