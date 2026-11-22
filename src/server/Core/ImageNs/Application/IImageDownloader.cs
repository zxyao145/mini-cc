namespace MiniCc.Api.Core.ImageNs.Application;

public enum DownloadOutcome
{
    Success,
    Permanent,
    Transient
}

public sealed record DownloadResult(
    DownloadOutcome Outcome,
    byte[]? Content = null,
    string? Ext = null,
    string? Reason = null,
    DateTimeOffset? RetryAfter = null);

public interface IImageDownloader
{
    Task<DownloadResult> DownloadAsync(string url, string articleUrl, CancellationToken cancellationToken);
}
