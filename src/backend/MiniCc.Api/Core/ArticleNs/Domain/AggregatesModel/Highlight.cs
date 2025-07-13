using MiniCc.Api.Shared.Data.Common;
using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;

public class Highlight : Entity
{
    [Required]
    public string Text { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string Color { get; set; } = "#FBBF24";

    public int StartOffset { get; set; }

    public int EndOffset { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid ArticleId { get; set; }

    public virtual Article Article { get; set; } = null!;



    private Highlight(Guid id, Guid articleId, string text, string note,
                     int startOffset, int endOffset) : base(id)
    {
        ArticleId = articleId;
        Text = text;
        Note = note;
        StartOffset = startOffset;
        EndOffset = endOffset;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Highlight() : base()
    {
    }

    public static Highlight Create(Guid articleId, string text, string note,
                                  int startOffset, int endOffset)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Selected text cannot be null or empty", nameof(text));

        if (startOffset < 0 || endOffset < 0 || startOffset >= endOffset)
            throw new ArgumentException("Invalid offset values");

        var id = UuidUtil.NewGuidV7();
        return new Highlight(id, articleId, text, note ?? string.Empty, startOffset, endOffset);
    }

    public void UpdateNote(string note)
    {
        Note = note ?? string.Empty;
    }
}