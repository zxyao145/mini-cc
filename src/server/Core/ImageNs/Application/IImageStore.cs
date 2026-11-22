namespace MiniCc.Api.Core.ImageNs.Application;

public interface IImageStore
{
    Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    Task<DateTimeOffset?> GetLastModifiedAsync(string key, CancellationToken cancellationToken);

    IAsyncEnumerable<StoredImage> ListAsync(CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public sealed record StoredImage(string Key, DateTimeOffset LastModified);
