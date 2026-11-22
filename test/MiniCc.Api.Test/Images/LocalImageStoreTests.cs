using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;

namespace MiniCc.Api.Test.Images;

public sealed class LocalImageStoreTests : ImageStoreContract, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"minicc-images-{Guid.NewGuid():N}");

    protected override IImageStore CreateStore() => new LocalImageStore(_root);

    [Fact]
    public async Task List_SkipsTempFiles()
    {
        var store = CreateStore();
        var key = Key();
        await store.PutAsync(key, [1], "image/png", CancellationToken.None);
        var tempPath = Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar) + ".interrupted.tmp");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
        await File.WriteAllBytesAsync(tempPath, [2]);

        var images = new List<StoredImage>();
        await foreach (var image in store.ListAsync(CancellationToken.None))
            images.Add(image);

        images.Should().ContainSingle(image => image.Key == key);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
