using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Domain;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;
using MiniCc.Api.Controllers.ArticleNs;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Sqlite")]
public sealed class ImageApiTests(SqliteFixture fixture)
{
    [Fact]
    public async Task Authenticated_NoCredentials_Returns401()
    {
        using var host = CreateHost(ImageDelivery.Authenticated);
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync($"/api/images/{Name("png")}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticated_ApiKey_Returns200WithHeaders()
    {
        using var host = CreateHost(ImageDelivery.Authenticated);
        await host.Store.Inner.PutAsync(ImageKeys.For(Hash, "png"), Png, "image/png", CancellationToken.None);
        using var client = SqliteApi.AuthenticatedClient(host.Factory);

        using var response = await client.GetAsync($"/api/images/{Name("png")}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.ETag!.Tag.Should().Be($"\"{Hash}\"");
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(31536000));
        response.Headers.CacheControl.ToString().Should().Contain("immutable");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle().Which.Should().Contain("sandbox");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
    }

    [Fact]
    public async Task Public_NoCredentials_Returns200WithPublicCacheControl()
    {
        using var host = CreateHost(ImageDelivery.Public);
        await host.Store.Inner.PutAsync(ImageKeys.For(Hash, "png"), Png, "image/png", CancellationToken.None);
        using var client = host.Factory.CreateClient();

        using var response = await client.GetAsync($"/api/images/{Name("png")}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(31536000));
        response.Headers.CacheControl.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task Svg_HasSandboxCsp()
    {
        using var host = CreateHost(ImageDelivery.Public);
        var svg = System.Text.Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        await host.Store.Inner.PutAsync(ImageKeys.For(Hash, "svg"), svg, "image/svg+xml", CancellationToken.None);
        using var client = host.Factory.CreateClient();

        using var response = await client.GetAsync($"/api/images/{Name("svg")}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        response.Headers.GetValues("Content-Security-Policy").Single().Should().Be("default-src 'none'; style-src 'unsafe-inline'; sandbox");
    }

    public static TheoryData<string> MalformedNames => new()
    {
        new string('A', 64) + ".png",
        "..%2f..%2fetc%2fpasswd",
        new string('a', 64) + ".exe",
        new string('a', 64),
        new string('a', 63) + ".png"
    };

    [Theory]
    [MemberData(nameof(MalformedNames))]
    public async Task Get_MalformedNames_Return404WithoutReadingStore(string name)
    {
        using var host = CreateHost(ImageDelivery.Authenticated);
        using var client = SqliteApi.AuthenticatedClient(host.Factory);

        using var response = await client.GetAsync($"/api/images/{name}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        host.Store.OpenReadCount.Should().Be(0);
    }

    [Fact]
    public async Task Get_MissingObject_Returns404()
    {
        using var host = CreateHost(ImageDelivery.Public);
        using var client = host.Factory.CreateClient();

        using var response = await client.GetAsync($"/api/images/{Name("png")}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        host.Store.OpenReadCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ImageDelivery.Authenticated)]
    [InlineData(ImageDelivery.Public)]
    public async Task ImageProxyMode_Returns404WithoutReadingStore(ImageDelivery delivery)
    {
        using var host = CreateHost(delivery, ImageMode.ImageProxy);
        using var client = host.Factory.CreateClient();

        using var response = await client.GetAsync($"/api/images/{Name("png")}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        host.Store.OpenReadCount.Should().Be(0);
    }

    private TestHost CreateHost(ImageDelivery delivery, ImageMode mode = ImageMode.Store)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"minicc-image-api-{Guid.NewGuid():N}");
        var imageStore = new CountingImageStore(new LocalImageStore(rootPath));
        var baseFactory = SqliteApi.CreateFactory(fixture);
        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Images:Mode"] = mode.ToString(),
                ["Images:Delivery"] = delivery.ToString(),
                ["Images:Storage:Provider"] = "Local",
                ["Images:Storage:LocalPath"] = rootPath
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IImageStore>();
                services.AddSingleton<IImageStore>(imageStore);
            });
        });
        return new TestHost(baseFactory, factory, imageStore, rootPath);
    }

    private static string Name(string ext) => $"{Hash}.{ext}";

    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly byte[] Png = [137, 80, 78, 71, 13, 10, 26, 10];

    private sealed class TestHost(
        WebApplicationFactory<ArticlesController> baseFactory,
        WebApplicationFactory<ArticlesController> factory,
        CountingImageStore store,
        string rootPath) : IDisposable
    {
        public WebApplicationFactory<ArticlesController> Factory { get; } = factory;
        public CountingImageStore Store { get; } = store;

        public void Dispose()
        {
            Factory.Dispose();
            baseFactory.Dispose();
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    private sealed class CountingImageStore(IImageStore inner) : IImageStore
    {
        private int openReadCount;

        public IImageStore Inner { get; } = inner;
        public int OpenReadCount => Volatile.Read(ref openReadCount);

        public Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken) =>
            Inner.PutAsync(key, content, contentType, cancellationToken);

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref openReadCount);
            return Inner.OpenReadAsync(key, cancellationToken);
        }

        public Task<DateTimeOffset?> GetLastModifiedAsync(string key, CancellationToken cancellationToken) =>
            Inner.GetLastModifiedAsync(key, cancellationToken);

        public IAsyncEnumerable<StoredImage> ListAsync(CancellationToken cancellationToken) => Inner.ListAsync(cancellationToken);

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Inner.DeleteAsync(key, cancellationToken);
    }
}
