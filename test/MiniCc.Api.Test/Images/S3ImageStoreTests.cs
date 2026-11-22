using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;
using Testcontainers.Minio;

namespace MiniCc.Api.Test.Images;

[CollectionDefinition("Minio")]
public sealed class MinioCollection : ICollectionFixture<MinioFixture>;

public sealed class MinioFixture : IAsyncLifetime
{
    private const string Bucket = "minicc-image-tests";
    private const string EmptyBucket = "minicc-image-empty-tests";
    private static readonly string Image = Environment.GetEnvironmentVariable("MINIO_TEST_IMAGE")
        ?? "cgr.dev/chainguard/minio:latest";

    private readonly MinioContainer _container = new MinioBuilder(Image)
        .WithUsername("minicc")
        .WithPassword("minicc-test-password")
        .Build();

    private IAmazonS3? _client;

    private IAmazonS3 Client => _client ?? throw new InvalidOperationException("MinIO has not started.");

    public async Task InitializeAsync()
    {
        using var startup = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await _container.StartAsync(startup.Token);
        _client = new AmazonS3Client(_container.GetAccessKey(), _container.GetSecretKey(), new AmazonS3Config
        {
            ServiceURL = _container.GetConnectionString(),
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        });
        await Client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket });
        await Client.PutBucketAsync(new PutBucketRequest { BucketName = EmptyBucket });
    }

    public IImageStore CreateStore() => new S3ImageStore(Client, Bucket);

    public IImageStore CreateEmptyStore() => new S3ImageStore(Client, EmptyBucket);

    public Task<GetObjectMetadataResponse> GetMetadataAsync(string key) =>
        Client.GetObjectMetadataAsync(Bucket, key);

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _container.DisposeAsync();
    }
}

[Collection("Minio")]
public sealed class S3ImageStoreTests(MinioFixture fixture) : ImageStoreContract
{
    protected override IImageStore CreateStore() => fixture.CreateStore();

    protected override Task<IImageStore> CreateEmptyStoreAsync() => Task.FromResult(fixture.CreateEmptyStore());

    [Fact]
    public async Task Put_StoresContentType()
    {
        var key = Key();
        await fixture.CreateStore().PutAsync(key, [1], "image/webp", CancellationToken.None);

        var metadata = await fixture.GetMetadataAsync(key);

        metadata.Headers.ContentType.Should().Be("image/webp");
    }
}
