using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Persistence;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;
using MiniCc.Api.Core.ImageNs.Infrastructure.Worker;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;
using MiniCc.Api.Test.Images;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class ImageWorkPostgresTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2002, 1, 2, 0, 0, 0, TimeSpan.Zero);
    private const string Image = "https://img.test/a.jpg";

    [Fact]
    public async Task Claim_ConcurrentWorkers_DoNotClaimSameArticle()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            var seeded = await fixture.SeedArticleAsync(a => a.SyncImages([Image], resetFailed: false, Now));
            ids.Add(seeded.Id);
        }

        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var claims = await Task.WhenAll(ImageLeases.ClaimDueAsync(first, Now, default), ImageLeases.ClaimDueAsync(second, Now, default));

        claims[0].Count.Should().BeLessThanOrEqualTo(10);
        claims[1].Count.Should().BeLessThanOrEqualTo(10);
        claims[0].Intersect(claims[1]).Should().BeEmpty();
        claims.SelectMany(claimed => claimed).Should().BeEquivalentTo(ids);
    }

    [Fact]
    public async Task ExpiredLease_IsReclaimed()
    {
        var earlier = Now.AddYears(-1);
        var seeded = await fixture.SeedArticleAsync(a => a.SyncImages([Image], resetFailed: false, earlier));
        await using var db = fixture.CreateContext();

        (await ImageLeases.ClaimDueAsync(db, earlier, default)).Should().Equal(seeded.Id);
        (await ImageLeases.ClaimDueAsync(db, earlier.AddMinutes(9), default)).Should().BeEmpty();
        (await ImageLeases.ClaimDueAsync(db, earlier.AddMinutes(10), default)).Should().Equal(seeded.Id);
        await db.Articles.Where(a => a.Id == seeded.Id).ExecuteUpdateAsync(update => update.SetProperty(a => a.ImagesDueAt, (DateTimeOffset?)null));
    }

    [Fact]
    public async Task Process_ConcurrentWriteback_PersistsOnlyOneResult()
    {
        var seeded = await fixture.SeedArticleAsync(a => a.SyncImages([Image], resetFailed: false, Now));
        await using var first = fixture.CreateContext();
        await using var second = fixture.CreateContext();
        var store = new LocalImageStore(fixture.ImageDirectory);
        var bothDownloading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloads = 0;
        byte[] firstBytes = [0xff, 0xd8, 0xff, 1];
        byte[] secondBytes = [0xff, 0xd8, 0xff, 2];

        var results = await Task.WhenAll(Process(first, firstBytes), Process(second, secondBytes));

        results.Should().BeEquivalentTo(new[] { new ImageProcessResult(1, 0, 0), new ImageProcessResult(0, 0, 0) });
        var winningBytes = results[0].Done == 1 ? firstBytes : secondBytes;
        var hash = Convert.ToHexStringLower(SHA256.HashData(winningBytes));
        await using var verification = fixture.CreateContext();
        var saved = await verification.Articles.SingleAsync(a => a.Id == seeded.Id);
        saved.Images![Image].Should().Be(new ArticleImage(ArticleImageState.Done, Hash: hash, Ext: "jpg"));
        saved.ImagesDueAt.Should().BeNull();
        await using var stream = await store.OpenReadAsync(ImageKeys.For(hash, "jpg"), default);
        using var output = new MemoryStream();
        await stream!.CopyToAsync(output);
        output.ToArray().Should().Equal(winningBytes);

        Task<ImageProcessResult> Process(MiniCcDbContext db, byte[] bytes)
        {
            var downloader = new Mock<IImageDownloader>();
            downloader.Setup(d => d.DownloadAsync(Image, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    if (Interlocked.Increment(ref downloads) == 2) bothDownloading.SetResult();
                    await bothDownloading.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    return new DownloadResult(DownloadOutcome.Success, bytes, "jpg");
                });
            var work = new ImageWorkService(db, new ImageArticleRows(db, new UnitOfWork(db)), TestImages.Legacy(), downloader.Object, store,
                NullLogger<ImageWorkService>.Instance);
            return work.ProcessAsync(seeded.Id, Now, default);
        }
    }
}
