using System.Net;
using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using MiniCc.Api.Core.ImageNs.Application;

namespace MiniCc.Api.Core.ImageNs.Infrastructure.Storage;

public sealed class S3ImageStore(IAmazonS3 client, string bucket) : IImageStore
{
    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        await using var input = new MemoryStream(content, writable: false);
        var serviceUrl = (client.Config as AmazonS3Config)?.ServiceURL;
        var disablePayloadSigning = Uri.TryCreate(serviceUrl, UriKind.Absolute, out var endpoint)
            ? endpoint.Scheme == Uri.UriSchemeHttps
            : !client.Config.UseHttp;
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = input,
            ContentType = contentType,
            UseChunkEncoding = false,
            DisablePayloadSigning = disablePayloadSigning,
            DisableDefaultChecksumValidation = disablePayloadSigning
        }, cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectAsync(bucket, key, cancellationToken);
            return new S3ObjectStream(response);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            return null;
        }
    }

    public async Task<DateTimeOffset?> GetLastModifiedAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetObjectMetadataAsync(bucket, key, cancellationToken);
            var lastModified = response.LastModified
                ?? throw new InvalidOperationException($"Object '{key}' has no last-modified timestamp.");
            return new DateTimeOffset(lastModified.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            return null;
        }
    }

    public async IAsyncEnumerable<StoredImage> ListAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        do
        {
            var response = await client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                ContinuationToken = continuationToken
            }, cancellationToken);

            foreach (var item in response.S3Objects ?? [])
            {
                var lastModified = item.LastModified
                    ?? throw new InvalidOperationException($"Object '{item.Key}' has no last-modified timestamp.");
                yield return new StoredImage(item.Key, new DateTimeOffset(lastModified.ToUniversalTime(), TimeSpan.Zero));
            }

            if (response.IsTruncated == true && string.IsNullOrEmpty(response.NextContinuationToken))
                throw new InvalidOperationException("S3 marked an object listing as truncated without a continuation token.");

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken is not null);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await client.DeleteObjectAsync(bucket, key, cancellationToken);
    }

    private static bool IsMissing(AmazonS3Exception exception) =>
        exception.StatusCode == HttpStatusCode.NotFound || exception.ErrorCode is "NoSuchKey" or "NotFound";

    // Disposing the returned stream must also release the SDK response that owns it.
    private sealed class S3ObjectStream(GetObjectResponse response) : Stream
    {
        private readonly Stream _stream = response.ResponseStream;

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => _stream.Position = value; }

        public override void Flush() => _stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _stream.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _stream.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void SetLength(long value) => _stream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            _stream.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                response.Dispose();
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            response.Dispose();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
