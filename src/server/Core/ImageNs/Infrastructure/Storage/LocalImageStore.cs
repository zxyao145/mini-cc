using System.Runtime.CompilerServices;
using MiniCc.Api.Core.ImageNs.Application;

namespace MiniCc.Api.Core.ImageNs.Infrastructure.Storage;

public sealed class LocalImageStore(string rootPath) : IImageStore
{
    private readonly string _rootPath = Path.GetFullPath(rootPath);

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (File.Exists(path))
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            return;
        }

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             bufferSize: 81920, FileOptions.Asynchronous))
            {
                await file.WriteAsync(content, cancellationToken);
            }

            try
            {
                File.Move(temporaryPath, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(key);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                81920, FileOptions.Asynchronous);
            return Task.FromResult<Stream?>(stream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task<DateTimeOffset?> GetLastModifiedAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(key);
        DateTimeOffset? lastModified = File.Exists(path)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
            : null;
        return Task.FromResult(lastModified);
    }

    public async IAsyncEnumerable<StoredImage> ListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_rootPath))
            yield break;

        await Task.Yield();
        foreach (var path in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;

            var key = Path.GetRelativePath(_rootPath, path).Replace(Path.DirectorySeparatorChar, '/');
            var lastModified = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            yield return new StoredImage(key, lastModified);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(key);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = Path.EndsInDirectorySeparator(_rootPath)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(rootWithSeparator, comparison))
            throw new ArgumentException("Image keys must resolve to a path beneath the storage root.", nameof(key));

        return path;
    }
}
