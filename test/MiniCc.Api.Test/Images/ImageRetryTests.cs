using MiniCc.Api.Core.ImageNs.Domain;

namespace MiniCc.Api.Test.Images;

public class ImageRetryTests
{
    [Fact]
    public void Schedule_FollowsSpec()
    {
        var now = DateTimeOffset.Parse("2026-11-21T00:00:00Z");
        var expectedMinutes = new[] { 1, 10, 60, 360, 1440, 1440, 1440, 1440, 1440 };

        for (var attempts = 1; attempts <= expectedMinutes.Length; attempts++)
            ImageRetry.NextAttempt(attempts, now, null).Should().Be(now.AddMinutes(expectedMinutes[attempts - 1]));

        ImageRetry.NextAttempt(10, now, null).Should().BeNull();
    }

    [Fact]
    public void RetryAfter_IsLowerBound()
    {
        var now = DateTimeOffset.Parse("2026-11-21T00:00:00Z");
        var retryAfter = now.AddHours(2);

        ImageRetry.NextAttempt(1, now, retryAfter).Should().Be(retryAfter);
    }
}
