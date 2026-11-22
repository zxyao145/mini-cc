using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Persistence;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;
using MiniCc.Api.Core.ImageNs.Infrastructure.Worker;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;
using MiniCc.Api.Test.Images;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public sealed class ImageWorkSqliteTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);
    private const string Image = "https://img.test/a.png";
    private const string Replacement = "https://img.test/b.png";
    private const string Html = $"<p>body</p><img src=\"{Image}\">";
    private static readonly byte[] Bytes = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly DownloadResult Success = new(DownloadOutcome.Success, Bytes, "png");
    private readonly SqliteFixture fixture = new();

    public Task InitializeAsync() => fixture.InitializeAsync();
    public Task DisposeAsync() => fixture.DisposeAsync();

    [Fact]
    public async Task Process_Success_StoresFileAndMarksDone()
    {
        var article = await SeedAsync(trashed: true);
        await using var db = fixture.CreateContext();
        var store = new LocalImageStore(Path.Combine(Path.GetDirectoryName(db.Database.GetDbConnection().DataSource)!, "images"));
        var work = Work(db, store: store);

        var result = await work.ProcessAsync(article.Id, Now, default);

        result.Should().Be(new ImageProcessResult(1, 0, 0));
        var saved = await ReadAsync(article.Id);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Bytes));
        saved.Images![Image].Should().Be(new ArticleImage(ArticleImageState.Done, Hash: hash, Ext: "png"));
        saved.ImagesDueAt.Should().BeNull();
        await using var stream = await store.OpenReadAsync(ImageKeys.For(hash, "png"), default);
        using var output = new MemoryStream();
        await stream!.CopyToAsync(output);
        output.ToArray().Should().Equal(Bytes);
    }

    [Theory]
    [InlineData(0, ArticleImageState.Pending)]
    [InlineData(9, ArticleImageState.Failed)]
    public async Task Process_Transient_RetriesUntilTenthFailure(int attempts, ArticleImageState expected)
    {
        var article = await SeedAsync(attempts: attempts);
        await using var db = fixture.CreateContext();
        var work = Work(db, () => Task.FromResult(new DownloadResult(DownloadOutcome.Transient, Reason: "timeout")));

        await work.ProcessAsync(article.Id, Now, default);

        var saved = await ReadAsync(article.Id);
        var image = saved.Images![Image];
        image.State.Should().Be(expected);
        if (expected == ArticleImageState.Pending)
        {
            image.Attempts.Should().Be(1);
            image.NextAttemptAt.Should().Be(Now.AddMinutes(1));
            saved.ImagesDueAt.Should().Be(image.NextAttemptAt);
        }
        else
        {
            image.Reason.Should().Be("timeout");
            saved.ImagesDueAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Process_StorageFailureSchedulesRetry()
    {
        var article = await SeedAsync();
        await using var db = fixture.CreateContext();
        var store = new Mock<IImageStore>();
        store.Setup(s => s.PutAsync(It.IsAny<string>(), Bytes, "image/png", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("unavailable"));

        await Work(db, store: store.Object).ProcessAsync(article.Id, Now, default);

        var saved = await ReadAsync(article.Id);
        saved.Images![Image].Should().Be(new ArticleImage(ArticleImageState.Pending, Attempts: 1, NextAttemptAt: Now.AddMinutes(1), Reason: "storage"));
    }

    [Fact]
    public async Task Process_ResaveDuringDownload_DiscardsStaleResultAndDownloadsReplacement()
    {
        var article = await SeedAsync();
        await using var db = fixture.CreateContext();
        var work = Work(db, async () =>
        {
            await using var edit = fixture.CreateContext();
            var existing = await edit.Articles.SingleAsync(a => a.Id == article.Id);
            existing.ReplaceContent(Content.Create("updated", $"<img src=\"{Replacement}\">"), ContentKind.Full);
            existing.SyncImages(resetFailed: true, Now);
            await edit.SaveChangesAsync();
            return Success;
        });

        (await work.ProcessAsync(article.Id, Now, default)).Should().Be(new ImageProcessResult(0, 0, 0));

        var saved = await ReadAsync(article.Id);
        saved.Images!.Keys.Should().Equal(Replacement);
        saved.Images[Replacement].State.Should().Be(ArticleImageState.Pending);
        saved.ImagesDueAt.Should().Be(Now);

        var downloader = new Mock<IImageDownloader>(MockBehavior.Strict);
        downloader.Setup(d => d.DownloadAsync(Replacement, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success);
        var replacementWork = new ImageWorkService(db, new ImageArticleRows(db, new UnitOfWork(db)), TestImages.Legacy(), downloader.Object,
            new LocalImageStore(fixture.ImageDirectory), NullLogger<ImageWorkService>.Instance);

        (await replacementWork.ProcessAsync(article.Id, Now, default)).Should().Be(new ImageProcessResult(1, 0, 0));
        downloader.Verify(d => d.DownloadAsync(Replacement, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        saved = await ReadAsync(article.Id);
        saved.Images!.Keys.Should().Equal(Replacement);
        saved.Images[Replacement].State.Should().Be(ArticleImageState.Done);
        saved.ImagesDueAt.Should().BeNull();
    }

    [Fact]
    public async Task Process_ClearsLeaseEvenWithoutDueEntries()
    {
        var next = Now.AddMinutes(5);
        var article = await fixture.SeedArticleAsync(a => a.SyncImages([Image], resetFailed: false, next));
        await using var db = fixture.CreateContext();
        await db.Articles.Where(a => a.Id == article.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.ImagesDueAt, Now.AddMinutes(10)));

        var work = Work(db, () => throw new InvalidOperationException("image is not due"));
        await work.ProcessAsync(article.Id, Now, default);

        (await ReadAsync(article.Id)).ImagesDueAt.Should().Be(next);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BackfillAndProcess_PreserveRevisionProgressAndHighlights(bool legacy)
    {
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.ReplaceContent(Content.Create("original", legacy ? TestImages.LegacyRewrite(Html) : Html, 4), ContentKind.Full);
            a.ApplyProgress(new(3, 1, 0.2, 0.7), "reader", Now.AddMinutes(-1));
            a.AddHighlight(Highlight.Create(a.Id, "body", "", 0, 4));
            if (!legacy) a.SyncImages([Image], resetFailed: false, Now);
        });
        await using var db = fixture.CreateContext();
        var work = Work(db);
        if (legacy)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Articles" SET "Images" = NULL WHERE "Id" = {article.Id}""");
            (await work.BackfillBatchAsync(Now, default)).Should().Be(1);
            (await work.BackfillBatchAsync(Now, default)).Should().Be(0);
        }
        else
        {
            await work.ProcessAsync(article.Id, Now, default);
        }

        var saved = await ReadAsync(article.Id);
        saved.ReadableContent.Should().Be(Html);
        saved.Length.Should().Be(article.Length);
        saved.ContentRevision.Should().Be(article.ContentRevision);
        saved.ProgressUpdatedAt.Should().Be(article.ProgressUpdatedAt);
        saved.ProgressTextOffset.Should().Be(article.ProgressTextOffset);
        saved.ProgressBlockIndex.Should().Be(article.ProgressBlockIndex);
        saved.ProgressBlockRatio.Should().Be(article.ProgressBlockRatio);
        saved.ProgressPercent.Should().Be(article.ProgressPercent);
        saved.Highlights.Should().ContainSingle().Which.AnchorStatus.Should().Be(AnchorStatus.Ok);
        saved.Images![Image].State.Should().Be(legacy ? ArticleImageState.Pending : ArticleImageState.Done);
    }

    [Fact]
    public async Task Claim_SetsLeaseAndSkipsLeasedIncludingTrash()
    {
        var article = await SeedAsync(trashed: true);
        await using var db = fixture.CreateContext();
        (await ImageLeases.ClaimDueAsync(db, Now, default)).Should().Equal(article.Id);
        (await ReadAsync(article.Id)).ImagesDueAt.Should().Be(Now.AddMinutes(10));
        (await ImageLeases.ClaimDueAsync(db, Now, default)).Should().BeEmpty();
    }

    private Task<Article> SeedAsync(bool trashed = false, int attempts = 0) => fixture.SeedArticleAsync(a =>
    {
        a.SyncImages([Image], resetFailed: false, Now);
        if (attempts > 0) a.ApplyImageResult(Image, new(ArticleImageState.Pending, Attempts: attempts, NextAttemptAt: Now));
        if (trashed) a.MoveToTrash(Now);
    });

    private async Task<Article> ReadAsync(Guid id)
    {
        await using var db = fixture.CreateContext();
        return await db.Articles.IgnoreQueryFilters().Include(a => a.Highlights).SingleAsync(a => a.Id == id);
    }

    private static ImageWorkService Work(MiniCcDbContext db, Func<Task<DownloadResult>>? download = null, IImageStore? store = null)
    {
        var downloader = new Mock<IImageDownloader>();
        downloader.Setup(d => d.DownloadAsync(Image, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(download ?? (() => Task.FromResult(Success)));

        return new(db, new ImageArticleRows(db, new UnitOfWork(db)), TestImages.Legacy(), downloader.Object, store ?? Mock.Of<IImageStore>(),
            NullLogger<ImageWorkService>.Instance);
    }
}
