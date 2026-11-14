namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

public enum ReadingStatus { Unread = 0, Reading = 1, Read = 2 }

public enum ContentKind { Full = 0, SummaryOnly = 1, Link = 2 }

public record ReadingPosition(int? TextOffset, int BlockIndex, double BlockRatio, double Percent);
