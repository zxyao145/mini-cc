using System.Net;
using System.Net.Http.Headers;
using System.Collections.Concurrent;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Download;

namespace MiniCc.Api.Test.Images;

public class ImageDownloaderTests
{
    [Fact]
    public async Task Download_Png_Success()
    {
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var downloader = CreateDownloader(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });

        var result = await downloader.DownloadAsync("https://images.example/a", "https://article.example/a", CancellationToken.None);

        result.Outcome.Should().Be(DownloadOutcome.Success);
        result.Content.Should().Equal(bytes);
        result.Ext.Should().Be("png");
    }

    [Fact]
    public async Task Download_FirstRequestHasNoReferer_RetriesWithArticleRefererOn403()
    {
        var calls = new List<HttpRequestMessage>();
        var downloader = CreateDownloader(request =>
        {
            calls.Add(CloneRequest(request));
            return calls.Count == 1
                ? new(HttpStatusCode.Forbidden)
                : new(HttpStatusCode.OK) { Content = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]) };
        });

        var result = await downloader.DownloadAsync("https://images.example/a", "https://article.example/story", CancellationToken.None);

        result.Outcome.Should().Be(DownloadOutcome.Success);
        calls.Should().HaveCount(2);
        calls[0].Headers.Referrer.Should().BeNull();
        calls[1].Headers.Referrer.Should().Be(new Uri("https://article.example/story"));
    }

    [Fact]
    public async Task Download_404_IsPermanentNotFound()
    {
        var downloader = CreateDownloader(_ => new(HttpStatusCode.NotFound));

        var result = await downloader.DownloadAsync("https://images.example/a", "https://article.example/", CancellationToken.None);

        result.Outcome.Should().Be(DownloadOutcome.Permanent);
        result.Reason.Should().Be("notFound");
    }

    [Fact]
    public async Task Download_503WithRetryAfter_IsTransient()
    {
        var downloader = CreateDownloader(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return response;
        });

        var before = DateTimeOffset.UtcNow;
        var result = await downloader.DownloadAsync("https://images.example/a", "https://article.example/", CancellationToken.None);

        result.Outcome.Should().Be(DownloadOutcome.Transient);
        result.Reason.Should().Be("http503");
        result.RetryAfter.Should().BeOnOrAfter(before.AddMinutes(4.9));
    }

    [Fact]
    public async Task Download_Html200_IsUnsupportedType()
    {
        var downloader = CreateDownloader(_ => new(HttpStatusCode.OK) { Content = new StringContent("<html>not an image</html>") });

        var result = await downloader.DownloadAsync("https://images.example/a.png", "https://article.example/", CancellationToken.None);

        result.Outcome.Should().Be(DownloadOutcome.Permanent);
        result.Reason.Should().Be("unsupportedType");
    }

    [Fact]
    public async Task Download_OverMaxBytes_IsTooLarge()
    {
        var downloader = CreateDownloader(_ => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[ImageDownloader.MaxBytes + 1])
        });

        var result = await downloader.DownloadAsync("https://images.example/a.png", "https://article.example/", CancellationToken.None);

        result.Outcome.Should().Be(DownloadOutcome.Permanent);
        result.Reason.Should().Be("tooLarge");
    }

    [Fact]
    public async Task Download_SendsUserAgentAndAccept()
    {
        HttpRequestMessage? sent = null;
        var downloader = CreateDownloader(request =>
        {
            sent = CloneRequest(request);
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]) };
        });

        await downloader.DownloadAsync("https://images.example/a", "https://article.example/", CancellationToken.None);

        sent!.Headers.UserAgent.ToString().Should().Be("Mozilla/5.0 (Macintosh; Intel Mac OS X 11_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.4280.141 Safari/537.36");
        sent.Headers.Accept.ToString().Should().Be("image/avif, image/webp, image/apng, image/*, */*; q=0.8");
    }

    [Fact]
    public async Task Download_InvalidUrl_IsPermanent()
    {
        var downloader = CreateDownloader(_ => throw new InvalidOperationException("No request expected"));

        var result = await downloader.DownloadAsync("file:///etc/passwd", "https://article.example/", CancellationToken.None);

        result.Outcome.Should().Be(DownloadOutcome.Permanent);
        result.Reason.Should().Be("invalidUrl");
    }

    [Fact]
    public async Task Download_RespectsPerHostConcurrency()
    {
        var activeByHost = new ConcurrentDictionary<string, int>();
        var maximumByHost = new ConcurrentDictionary<string, int>();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("images")).Returns(() => new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            var host = request.RequestUri!.IdnHost;
            var hostCount = activeByHost.AddOrUpdate(host, 1, static (_, current) => current + 1);
            UpdateMaximum(maximumByHost, host, hostCount);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken);
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]) };
            }
            finally
            {
                activeByHost.AddOrUpdate(host, 0, static (_, current) => current - 1);
            }
        })));
        var downloader = new ImageDownloader(factory.Object);
        var urls = Enumerable.Range(0, 9).Select(index => $"https://images-{index % 3}.example/{index}");

        var results = await Task.WhenAll(urls.Select(url => downloader.DownloadAsync(url, "https://article.example/", CancellationToken.None)));

        results.Should().OnlyContain(result => result.Outcome == DownloadOutcome.Success);
        maximumByHost.Values.Should().OnlyContain(count => count <= 2);
        maximumByHost.Values.Should().Contain(2);
    }

    [Fact]
    public async Task Download_DeadlineCoversResponseBodyAndStartsAfterHostQueue()
    {
        var timers = new List<Action>();
        var clock = new Mock<TimeProvider>();
        clock.Setup(p => p.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object>(),
                TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan))
            .Returns((TimerCallback callback, object state, TimeSpan _, TimeSpan _) =>
            {
                timers.Add(() => callback(state));
                return Mock.Of<ITimer>();
            });

        var reads = 0;
        using var handler = new StubHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath == "/queued"
                ? new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10])
                : new StreamContent(new SlowBody(() => reads++))
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("images")).Returns(() => new HttpClient(handler, disposeHandler: false));
        var downloader = new ImageDownloader(factory.Object, clock.Object);
        var first = Download("first");
        var second = Download("second");
        var queued = Download("queued");
        var activeTimers = timers.ToArray();
        var activeReads = reads;
        foreach (var expire in activeTimers) expire();

        var results = await Task.WhenAll(first, second, queued).WaitAsync(TimeSpan.FromSeconds(5));

        activeReads.Should().Be(2);
        activeTimers.Should().HaveCount(2);
        results.Take(2).Should().OnlyContain(result => result.Outcome == DownloadOutcome.Transient && result.Reason == "timeout");
        results[2].Outcome.Should().Be(DownloadOutcome.Success);
        timers.Should().HaveCount(3);

        Task<DownloadResult> Download(string path) =>
            downloader.DownloadAsync($"https://slow-images.example/{path}", "https://article.example/", default);
    }

    private static ImageDownloader CreateDownloader(Func<HttpRequestMessage, HttpResponseMessage> send)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient("images")).Returns(() => new HttpClient(new StubHandler(send)));
        return new ImageDownloader(factory.Object);
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Headers.Referrer is { } referer) clone.Headers.Referrer = referer;
        if (request.Headers.UserAgent.Count > 0) clone.Headers.UserAgent.ParseAdd(request.Headers.UserAgent.ToString());
        if (request.Headers.Accept.Count > 0) clone.Headers.Accept.ParseAdd(request.Headers.Accept.ToString());
        return clone;
    }

    private static void UpdateMaximum(ConcurrentDictionary<string, int> maximum, string key, int value) =>
        maximum.AddOrUpdate(key, value, (_, current) => Math.Max(current, value));

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
            : this((request, _) => Task.FromResult(send(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class SlowBody(Action onRead) : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            onRead();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
