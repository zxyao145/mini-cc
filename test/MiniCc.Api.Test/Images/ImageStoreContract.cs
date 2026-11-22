using MiniCc.Api.Core.ImageNs.Application;

namespace MiniCc.Api.Test.Images;

public abstract class ImageStoreContract
{
    protected abstract IImageStore CreateStore();

    protected virtual Task<IImageStore> CreateEmptyStoreAsync() => Task.FromResult(CreateStore());

    [Fact]
    public async Task Put_ThenOpen_ReturnsSameBytes()
    {
        var store = CreateStore();
        var content = new byte[] { 1, 2, 3, 4 };
        var key = Key();

        await store.PutAsync(key, content, "image/png", CancellationToken.None);
        await using var stream = await store.OpenReadAsync(key, CancellationToken.None);
        using var result = new MemoryStream();
        await stream!.CopyToAsync(result);

        result.ToArray().Should().Equal(content);
    }

    [Fact]
    public async Task Open_Missing_ReturnsNull()
    {
        var stream = await CreateStore().OpenReadAsync(Key(), CancellationToken.None);

        stream.Should().BeNull();
    }

    [Fact]
    public async Task Put_Existing_RefreshesLastModified()
    {
        var store = CreateStore();
        var key = Key();
        await store.PutAsync(key, [1], "image/png", CancellationToken.None);
        var firstModified = await store.GetLastModifiedAsync(key, CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1.1));

        await store.PutAsync(key, [1], "image/png", CancellationToken.None);
        var secondModified = await store.GetLastModifiedAsync(key, CancellationToken.None);

        secondModified.Should().BeAfter(firstModified!.Value);
    }

    [Fact]
    public async Task List_ReturnsKeysWithForwardSlashes()
    {
        var store = CreateStore();
        var key = Key();
        await store.PutAsync(key, [1], "image/png", CancellationToken.None);

        var images = new List<StoredImage>();
        await foreach (var image in store.ListAsync(CancellationToken.None))
            images.Add(image);

        images.Should().Contain(image => image.Key == key);
        images.Should().OnlyContain(image => !image.Key.Contains('\\'));
    }

    [Fact]
    public async Task List_EmptyStore_ReturnsNoObjects()
    {
        var store = await CreateEmptyStoreAsync();
        var images = new List<StoredImage>();

        await foreach (var image in store.ListAsync(CancellationToken.None))
            images.Add(image);

        images.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_RemovesObject()
    {
        var store = CreateStore();
        var key = Key();
        await store.PutAsync(key, [1], "image/png", CancellationToken.None);

        await store.DeleteAsync(key, CancellationToken.None);

        (await store.GetLastModifiedAsync(key, CancellationToken.None)).Should().BeNull();
    }

    protected static string Key() => $"aa/{Guid.NewGuid():N}.png";
}
