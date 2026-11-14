using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data.Common;

namespace MiniCc.Api.Core.FeedNs.Domain;

public class FeedEntry : Entity
{
    public Guid FeedId { get; set; }
    public Feed Feed { get; set; } = null!;
    public string EntryKey { get; private set; } = "";
    public string EntryKeyHash { get; private set; } = "";
    public string? EntryUrl { get; set; }
    public Guid? ArticleId { get; set; }
    public Article? Article { get; set; }
    public string ContentHash { get; set; } = "";
    public DateTimeOffset FirstSeenAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Dismissed { get; set; }
    public FeedEntry() { }
    private FeedEntry(Guid id) : base(id) { }
    public static FeedEntry Create(Guid feedId, string key, string url, string hash) => new(UuidUtil.NewGuidV7())
        { FeedId = feedId, EntryKey = key, EntryKeyHash = FeedIdentity.Hash(key), EntryUrl = url, ContentHash = hash };
}
