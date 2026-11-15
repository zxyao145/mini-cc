namespace MiniCc.Api.Core.FeedNs.Domain;

public static class FeedText
{
    // Storage limits use UTF-16 lengths. Never cut between the two halves of a Unicode scalar.
    public static string Limit(string value, int maximum)
    {
        if (value.Length <= maximum) return value;
        var length = maximum;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length])) length--;
        return value[..length];
    }
}
