using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

public enum ArticleImageState { Pending, Done, Failed }

public sealed record ArticleImage(
    ArticleImageState State,
    string? Hash = null,
    string? Ext = null,
    int Attempts = 0,
    [property: JsonPropertyName("next")] DateTimeOffset? NextAttemptAt = null,
    string? Reason = null);
