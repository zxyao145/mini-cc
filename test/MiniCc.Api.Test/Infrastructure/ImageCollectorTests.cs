using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Application.Services;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Persistence;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Infrastructure;

public sealed class ImageCollectorTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);
    private const string ImageAddress = "https://example.com/image.png";
    private const string MissingImageAddress = "https://example.com/missing.png";
    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private readonly SqliteFixture _fixture = new();
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"minicc-image-collector-{Guid.NewGuid():N}");
    private readonly List<SqliteMiniCcDbContext> _contexts = [];
    private LocalImageStore _store = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _store = new LocalImageStore(_storageRoot);
    }

    public async Task DisposeAsync()
    {
        foreach (var context in _contexts)
            await context.DisposeAsync();
        await _fixture.DisposeAsync();
        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);
    }

    [Fact]
    public async Task SkipsWhenAnyArticleHasNullImages()
    {
        var article = await SeedArticleAsync();
        var key = await PutOldImageAsync(new('b', 64));
        await using (var db = _fixture.CreateContext())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"Images\" = NULL WHERE \"Id\" = {article.Id}");

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Should().Be(new CollectResult(Skipped: true, Deleted: 0, Repaired: 0));
        (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task SkipsInImageProxyMode()
    {
        var key = await PutOldImageAsync(new('b', 64));

        var result = await CreateCollector(ImageMode.ImageProxy).CollectAsync(Now, CancellationToken.None);

        result.Should().Be(new CollectResult(Skipped: true, Deleted: 0, Repaired: 0));
        (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeletesOldUnreferencedFiles()
    {
        var key = await PutOldImageAsync(new('b', 64));

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Should().Be(new CollectResult(Skipped: false, Deleted: 1, Repaired: 0));
        (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task KeepsFilesYoungerThanGrace()
    {
        var hash = new string('b', 64);
        var key = ImageKeys.For(hash, "png");
        await _store.PutAsync(key, [1, 2, 3], "image/png", CancellationToken.None);
        SetLastModified(key, Now.AddHours(-1));

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Deleted.Should().Be(0);
        (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task KeepsFilesReferencedByTrashedArticles()
    {
        var key = ImageKeys.For(Hash, "png");
        await _store.PutAsync(key, [1, 2, 3], "image/png", CancellationToken.None);
        SetLastModified(key, Now.AddDays(-2));
        var article = CreateDoneArticle(Hash);
        article.MoveToTrash(Now.AddDays(-1));
        await SaveArticleAsync(article);

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Deleted.Should().Be(0);
        (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task KeepsForeignKeys()
    {
        var validHash = new string('b', 64);
        var mismatchedDirectoryKey = $"aa/{validHash}.png";
        var invalidKeys = new[] { "notes.txt", "ab/xyz.png", mismatchedDirectoryKey };
        foreach (var key in invalidKeys)
        {
            await _store.PutAsync(key, [1], "image/png", CancellationToken.None);
            SetLastModified(key, Now.AddDays(-2));
        }

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Deleted.Should().Be(0);
        foreach (var key in invalidKeys)
            (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task RechecksLastModifiedBeforeDelete()
    {
        var key = await PutOldImageAsync(new('b', 64));
        var store = new LastModifiedChangingImageStore(
            _store,
            key,
            Now.AddDays(-2),
            Now.AddHours(-1));

        var result = await CreateCollector(store).CollectAsync(Now, CancellationToken.None);

        result.Deleted.Should().Be(0);
        (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task RepairsDoneEntriesWithMissingFiles()
    {
        var article = CreateDoneArticle(Hash);
        await SaveArticleAsync(article);

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Should().Be(new CollectResult(Skipped: false, Deleted: 0, Repaired: 1));
        await using var db = _fixture.CreateContext();
        var repaired = await db.Articles.SingleAsync(a => a.Id == article.Id);
        repaired.Images![ImageAddress].Should().Be(new ArticleImage(ArticleImageState.Pending, NextAttemptAt: Now));
        repaired.ImagesDueAt.Should().Be(Now);
    }

    [Fact]
    public async Task ScansAndRepairsArticlesBeyondTheBatchBoundary()
    {
        var storedHash = new string('b', 64);
        var missingHash = new string('c', 64);
        var storedKey = await PutOldImageAsync(storedHash);
        var articles = Enumerable.Range(0, 201).Select(CreateArticle).ToArray();
        await SaveArticlesAsync(articles);

        Guid lastArticleId;
        await using (var db = _fixture.CreateContext())
        {
            var lastArticle = await db.Articles.OrderBy(article => article.Id).LastAsync();
            lastArticleId = lastArticle.Id;
            lastArticle.SyncImages([ImageAddress, MissingImageAddress], resetFailed: false, Now);
            lastArticle.ApplyImageResult(ImageAddress, new ArticleImage(ArticleImageState.Done, storedHash, "png"));
            lastArticle.ApplyImageResult(MissingImageAddress, new ArticleImage(ArticleImageState.Done, missingHash, "png"));
            await db.SaveChangesAsync();
        }

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Should().Be(new CollectResult(Skipped: false, Deleted: 0, Repaired: 1));
        (await _store.GetLastModifiedAsync(storedKey, CancellationToken.None)).Should().NotBeNull();
        await using var verification = _fixture.CreateContext();
        var repaired = await verification.Articles.SingleAsync(article => article.Id == lastArticleId);
        repaired.Images![ImageAddress].State.Should().Be(ArticleImageState.Done);
        repaired.Images[MissingImageAddress].Should().Be(new ArticleImage(ArticleImageState.Pending, NextAttemptAt: Now));
    }

    [Fact]
    public async Task PurgedArticleImagesAreDeletedOnNextRun()
    {
        var key = ImageKeys.For(Hash, "png");
        await _store.PutAsync(key, [1, 2, 3], "image/png", CancellationToken.None);
        SetLastModified(key, Now.AddDays(-2));
        var article = CreateDoneArticle(Hash);
        article.MoveToTrash(Now.AddDays(-2));
        await SaveArticleAsync(article);

        await using (var db = _fixture.CreateContext())
        {
            var unitOfWork = new UnitOfWork(db);
            var purger = new ArticlePurger(db, unitOfWork);
            (await purger.PurgeAsync(article.Id, expiredBefore: null, CancellationToken.None)).Should().BeNull();
        }

        var result = await CreateCollector().CollectAsync(Now, CancellationToken.None);

        result.Should().Be(new CollectResult(Skipped: false, Deleted: 1, Repaired: 0));
        (await _store.GetLastModifiedAsync(key, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SkipsWhenListingFails()
    {
        var store = new FailingListImageStore(_store);

        var result = await CreateCollector(store).CollectAsync(Now, CancellationToken.None);

        result.Should().Be(new CollectResult(Skipped: true, Deleted: 0, Repaired: 0));
    }

    private ImageCollector CreateCollector(ImageMode mode = ImageMode.Store) =>
        CreateCollector(_store, mode);

    private ImageCollector CreateCollector(IImageStore store, ImageMode mode = ImageMode.Store)
    {
        var db = _fixture.CreateContext();
        _contexts.Add(db);
        return new ImageCollector(
            db,
            new ImageArticleRows(db, new UnitOfWork(db)),
            store,
            Options.Create(new ImageOptions { Mode = mode }),
            NullLogger<ImageCollector>.Instance);
    }

    private async Task<Article> SeedArticleAsync()
    {
        var article = Article.Create(
            Url.Create($"https://example.com/{Guid.NewGuid():N}"),
            "Image collector test",
            "",
            Content.Create("original", "readable"));
        await SaveArticleAsync(article);
        return article;
    }

    private async Task SaveArticleAsync(Article article)
    {
        await using var db = _fixture.CreateContext();
        db.Articles.Add(article);
        await db.SaveChangesAsync();
    }

    private async Task SaveArticlesAsync(IEnumerable<Article> articles)
    {
        await using var db = _fixture.CreateContext();
        db.Articles.AddRange(articles);
        await db.SaveChangesAsync();
    }

    private static Article CreateArticle(int index) =>
        Article.Create(
            Url.Create($"https://example.com/{index}-{Guid.NewGuid():N}"),
            "Image collector batch test",
            "",
            Content.Create("original", "readable"));

    private static Article CreateDoneArticle(string hash)
    {
        var article = Article.Create(
            Url.Create($"https://example.com/{Guid.NewGuid():N}"),
            "Image collector test",
            "",
            Content.Create("original", "readable"));
        article.SyncImages([ImageAddress], resetFailed: false, Now);
        article.ApplyImageResult(ImageAddress, new ArticleImage(ArticleImageState.Done, hash, "png"));
        return article;
    }

    private async Task<string> PutOldImageAsync(string hash)
    {
        var key = ImageKeys.For(hash, "png");
        await _store.PutAsync(key, [1, 2, 3], "image/png", CancellationToken.None);
        SetLastModified(key, Now.AddDays(-2));
        return key;
    }

    private void SetLastModified(string key, DateTimeOffset value) =>
        File.SetLastWriteTimeUtc(Path.Combine(_storageRoot, key.Replace('/', Path.DirectorySeparatorChar)), value.UtcDateTime);

    private sealed class LastModifiedChangingImageStore(
        IImageStore inner,
        string key,
        DateTimeOffset listedTime,
        DateTimeOffset currentTime) : IImageStore
    {
        public Task PutAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken) =>
            inner.PutAsync(objectKey, content, contentType, cancellationToken);

        public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken) =>
            inner.OpenReadAsync(objectKey, cancellationToken);

        public Task<DateTimeOffset?> GetLastModifiedAsync(string objectKey, CancellationToken cancellationToken) =>
            objectKey == key ? Task.FromResult<DateTimeOffset?>(currentTime) : inner.GetLastModifiedAsync(objectKey, cancellationToken);

        public async IAsyncEnumerable<StoredImage> ListAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var image in inner.ListAsync(cancellationToken))
                yield return image.Key == key ? image with { LastModified = listedTime } : image;
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) =>
            inner.DeleteAsync(objectKey, cancellationToken);
    }

    private sealed class FailingListImageStore(IImageStore inner) : IImageStore
    {
        public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken) =>
            inner.PutAsync(key, content, contentType, cancellationToken);

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
            inner.OpenReadAsync(key, cancellationToken);

        public Task<DateTimeOffset?> GetLastModifiedAsync(string key, CancellationToken cancellationToken) =>
            inner.GetLastModifiedAsync(key, cancellationToken);

        public IAsyncEnumerable<StoredImage> ListAsync(CancellationToken cancellationToken) =>
            throw new IOException("list failed");

        public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
            inner.DeleteAsync(key, cancellationToken);
    }
}
