using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;

namespace MiniCc.Api.Test.Core.ArticleNs.Domain;

public class ArticleImagesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);
    private const string First = "https://img.test/a.png";
    private const string Second = "https://img.test/b.png";
    private const string Third = "https://img.test/c.png";

    [Fact]
    public void Create_StartsWithEmptyImagesAndNoDueTime()
    {
        var article = Create();
        article.Images.Should().BeEmpty();
        article.ImagesDueAt.Should().BeNull();
    }

    [Fact]
    public void SyncImages_AddsPending_RemovesMissing_KeepsDone()
    {
        var article = Create();
        var done = new ArticleImage(ArticleImageState.Done, Hash: "hash", Ext: "png");
        article.SyncImages([First, Second], resetFailed: false, Now);
        article.ApplyImageResult(First, done);

        article.SyncImages([First, Third], resetFailed: true, Now.AddHours(1));

        article.Images!.Should().BeEquivalentTo(new Dictionary<string, ArticleImage>
        {
            [First] = done, [Third] = new(ArticleImageState.Pending, NextAttemptAt: Now.AddHours(1))
        });
    }

    [Fact]
    public void SyncImages_ResetFailed_OnlyWhenRequested()
    {
        var article = Create();
        var failed = new ArticleImage(ArticleImageState.Failed, Reason: "notFound");
        article.SyncImages([First], resetFailed: false, Now);
        article.ApplyImageResult(First, failed);
        article.SyncImages([First], resetFailed: false, Now);

        article.Images![First].Should().Be(failed);
        article.ImagesDueAt.Should().BeNull();

        article.SyncImages([First], resetFailed: true, Now);

        article.Images![First].Should().Be(new ArticleImage(ArticleImageState.Pending, NextAttemptAt: Now));
        article.ImagesDueAt.Should().Be(Now);
    }

    [Fact]
    public void ApplyImageResult_UsesEarliestNextAndIgnoresMissingOrCompletedEntries()
    {
        var article = Create();
        article.SyncImages([First, Second], resetFailed: false, Now);
        var retry = new ArticleImage(ArticleImageState.Pending, Attempts: 1, NextAttemptAt: Now.AddMinutes(1));

        article.ApplyImageResult(First, retry).Should().BeTrue();
        article.ImagesDueAt.Should().Be(Now);

        var done = new ArticleImage(ArticleImageState.Done, Hash: "hash", Ext: "png");
        article.ApplyImageResult(Second, done).Should().BeTrue();
        article.ImagesDueAt.Should().Be(retry.NextAttemptAt);
        article.ApplyImageResult(Second, retry).Should().BeFalse();
        article.ApplyImageResult(Third, retry).Should().BeFalse();
        article.Images![Second].Should().Be(done);
    }

    [Fact]
    public void NormalizeLegacyImages_KeepsRevisionProgressAndLength()
    {
        var article = Create();
        article.ApplyProgress(new(4, 2, 0.4, 0.6), "reader", Now);
        var revision = article.ContentRevision;
        var length = article.Length;

        article.NormalizeLegacyImages("<p>body</p>", First, Now.AddMinutes(3));

        article.ReadableContent.Should().Be("<p>body</p>");
        article.ImageUrl.Should().Be(First);
        article.OriginalContent.Should().Be("original");
        article.Length.Should().Be(length);
        article.ContentRevision.Should().Be(revision);
        article.ProgressTextOffset.Should().Be(4);
        article.ProgressBlockIndex.Should().Be(2);
        article.ProgressBlockRatio.Should().Be(0.4);
        article.ProgressPercent.Should().Be(0.6);
        article.ProgressSessionId.Should().Be("reader");
        article.ProgressUpdatedAt.Should().Be(Now);
        article.LastReadAt.Should().Be(Now);
    }

    [Fact]
    public void ResetMissingImages_ResetsOnlyMatchingDoneEntries()
    {
        var article = Create();
        article.SyncImages([First, Second, Third], resetFailed: false, Now);
        article.ApplyImageResult(First, new(ArticleImageState.Done, Hash: "missing", Ext: "png"));
        var present = new ArticleImage(ArticleImageState.Done, Hash: "present", Ext: "png");
        article.ApplyImageResult(Second, present);
        var failed = new ArticleImage(ArticleImageState.Failed, Reason: "notFound");
        article.ApplyImageResult(Third, failed);

        article.ResetMissingImages(new HashSet<string> { "missing" }, Now).Should().Be(1);

        article.Images![First].Should().Be(new ArticleImage(ArticleImageState.Pending, NextAttemptAt: Now));
        article.Images[Second].Should().Be(present);
        article.Images[Third].Should().Be(failed);
        article.ImagesDueAt.Should().Be(Now);
    }

    private static Article Create() =>
        Article.Create(Url.Create("https://example.com/article"), "Article", "", Content.Create("original", "body", 4));
}
