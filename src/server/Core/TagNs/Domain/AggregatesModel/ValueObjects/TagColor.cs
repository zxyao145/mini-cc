using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;

public class TagColor : ValueObject
{
    public string Value { get; private set; }

    private TagColor(string value)
    {
        Value = value;
    }

    public static TagColor Create(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return Default;

        if (!IsValidHexColor(color))
            throw new ArgumentException("Invalid color format. Must be a valid hex color.", nameof(color));

        return new TagColor(color);
    }

    public static TagColor Default = new("#3B82F6");

    public static bool IsValidHexColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color[1..].All(char.IsAsciiHexDigit);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator string(TagColor color) => color.Value;

    public override string ToString() => Value;
}