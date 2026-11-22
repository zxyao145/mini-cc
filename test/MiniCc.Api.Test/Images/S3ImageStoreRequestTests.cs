using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure;
using MiniCc.Api.Core.ImageNs.Infrastructure.Storage;

namespace MiniCc.Api.Test.Images;

public sealed class S3ImageStoreRequestTests
{
    [Fact]
    public async Task Put_OverHttps_SendsUnsignedPayloadWithoutAwsChunkedEncoding()
    {
        const string endpoint = "https://r2.example.invalid";
        const string bucket = "images-test";
        const string key = "aa/image.png";
        byte[] content = [1, 2, 3, 4];
        var requests = 0;
        using var transport = new PutHandler(async (request, cancellationToken) =>
        {
            requests++;
            request.Method.Should().Be(HttpMethod.Put);
            request.RequestUri!.AbsoluteUri.Should().Be($"{endpoint}/{bucket}/{key}");
            request.Headers.GetValues("x-amz-content-sha256").Should().Equal("UNSIGNED-PAYLOAD");
            request.Content!.Headers.ContentType!.MediaType.Should().Be("image/png");
            request.Content.Headers.ContentEncoding.Should().NotContain("aws-chunked");
            (await request.Content.ReadAsByteArrayAsync(cancellationToken)).Should().Equal(content);
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Images:Storage:Provider"] = "S3",
            ["Images:Storage:S3:ServiceUrl"] = endpoint,
            ["Images:Storage:S3:Bucket"] = bucket,
            ["Images:Storage:S3:AccessKeyId"] = "test-access-key",
            ["Images:Storage:S3:SecretAccessKey"] = "test-secret-key"
        }).Build();
        using var services = new ServiceCollection().AddImageServices(configuration, AppContext.BaseDirectory).BuildServiceProvider();
        var config = services.GetRequiredService<IAmazonS3>().Config.Should().BeOfType<AmazonS3Config>().Subject;
        config.ForcePathStyle.Should().BeTrue();
        config.AuthenticationRegion.Should().Be("auto");
        config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_REQUIRED);
        config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_REQUIRED);
        config.HttpClientFactory = new TestHttpClientFactory(transport);

        var store = services.GetRequiredService<IImageStore>().Should().BeOfType<S3ImageStore>().Subject;
        await store.PutAsync(key, content, "image/png", CancellationToken.None);
        requests.Should().Be(1);
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler transport) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(transport, disposeHandler: false);
    }

    private sealed class PutHandler(Func<HttpRequestMessage, CancellationToken, Task> assertRequest) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await assertRequest(request, cancellationToken);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        }
    }
}
