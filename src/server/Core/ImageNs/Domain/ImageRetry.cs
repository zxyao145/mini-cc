namespace MiniCc.Api.Core.ImageNs.Domain;

public static class ImageRetry
{
    public const int MaxAttempts = 10;

    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(24)
    ];

    public static DateTimeOffset? NextAttempt(int attempts, DateTimeOffset now, DateTimeOffset? retryAfter)
    {
        if (attempts >= MaxAttempts)
            return null;

        var delay = Delays[Math.Clamp(attempts - 1, 0, Delays.Length - 1)];
        var next = now + delay;
        return retryAfter is { } minimum && minimum > next ? minimum : next;
    }
}
