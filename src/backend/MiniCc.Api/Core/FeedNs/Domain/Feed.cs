using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.FeedNs.Domain;

public class Feed : AggregateRoot
{
    public string FeedUrl { get; private set; } = "";
    public string FeedUrlHash { get; private set; } = "";
    public string? SiteUrl { get; set; }
    public string OriginalTitle { get; set; } = "";
    public string Title { get; set; } = "";
    public FeedFormat Format { get; set; }
    public FeedStatus Status { get; set; }
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public DateTimeOffset? LastFetchedAt { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int MinIntervalMinutes { get; set; } = 30;
    public DateTimeOffset NextFetchAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
    public ICollection<FeedEntry> Entries { get; set; } = new List<FeedEntry>();
    public Feed() { }
    private Feed(Guid id) : base(id) { }
    public static Feed Create(string url, string title, string? siteUrl, FeedFormat format)
    {
        var normalized = UrlNormalizer.Normalize(url);
        return new(UuidUtil.NewGuidV7()) { FeedUrl = normalized, FeedUrlHash = FeedIdentity.Hash(normalized), Title = title, OriginalTitle = title, SiteUrl = siteUrl, Format = format };
    }
}
