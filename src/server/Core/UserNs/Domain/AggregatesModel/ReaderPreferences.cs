namespace MiniCc.Api.Core.UserNs.Domain.AggregatesModel;

public record ReaderPreferences
{
    public int FontSize { get; init; } = 18;
    public double LineHeight { get; init; } = 1.8;
    public string Width { get; init; } = "standard";
    public string FontFamily { get; init; } = "system";
    public string Theme { get; init; } = "system";
}
