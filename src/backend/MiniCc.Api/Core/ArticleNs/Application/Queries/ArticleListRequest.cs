using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ArticleNs.Application.Queries;

public class ArticleListRequest
{
    public string View { get; set; } = "inbox";
    public string? Q { get; set; }
    public string? Status { get; set; }
    public string? Tags { get; set; }
    public string? Source { get; set; }
    public Guid? FeedId { get; set; }
    public DateTimeOffset? SavedFrom { get; set; }
    public DateTimeOffset? SavedTo { get; set; }
    public string Sort { get; set; } = "saved_desc";
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 20;
    internal string EffectiveSort => View == "trash" ? "trash" : Sort;
    internal Guid[] TagIds => Split(Tags).Select(Guid.Parse).Distinct().ToArray();
    internal ReadingStatus[] Statuses => Split(Status).Select(s => Enum.Parse<ReadingStatus>(s, true)).Distinct().ToArray();
    private static string[] Split(string? value) => string.IsNullOrWhiteSpace(value) ? [] : value.Split(',', StringSplitOptions.TrimEntries);

    public void Validate()
    {
        if (View is not ("inbox" or "all" or "favorites" or "archive" or "trash" or "feeds")) throw new ArgumentException("Unsupported library view.");
        if (Sort is not ("saved_desc" or "saved_asc" or "read_desc" or "published_desc") && View != "trash") throw new ArgumentException("Unsupported sort.");
        if (Q?.Length > 200) throw new ArgumentException("Search must be at most 200 characters.");
        if (Split(Status).Any(s => s is not ("unread" or "reading" or "read"))) throw new ArgumentException("Invalid reading status.");
        if (Split(Tags).Length > 10 || Split(Tags).Any(s => !Guid.TryParse(s, out var id) || id == Guid.Empty)) throw new ArgumentException("Provide at most 10 valid tag IDs.");
        if (Source is not (null or "" or "extension" or "feed")) throw new ArgumentException("Invalid source.");
        if (FeedId == Guid.Empty) throw new ArgumentException("Invalid feed ID.");
        if (PageSize is < 1 or > 50) throw new ArgumentException("Page size must be between 1 and 50.");
        if (SavedFrom.HasValue && SavedTo.HasValue && SavedFrom >= SavedTo) throw new ArgumentException("Saved date range must have an end after its start.");
    }
}
