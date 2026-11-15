using System.Net;
using System.Net.Sockets;
using System.Text;
using MiniCc.Api.Core.FeedNs.Infrastructure;

namespace MiniCc.Api.Test.Feeds;

public class FeedFetcherTests
{
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
    [Fact]
    public async Task ConditionalRequests_AndDiscoveryAreBounded()
    {
        var requests = new List<string>();
        var fetcher = new FeedFetcher(new Factory(new Handler(r =>
        {
            requests.Add(r.RequestUri!.AbsoluteUri);
            if (r.RequestUri.AbsolutePath == "/") return new(HttpStatusCode.OK) { Content = new StringContent("<html><link rel='alternate' type='application/atom+xml' href='/feed'></html>", Encoding.UTF8, "text/html") };
            r.Headers.IfNoneMatch.ToString().Should().Be("\"version\"");
            return new(HttpStatusCode.NotModified);
        })));
        var fetched = await fetcher.FetchAsync("http://localhost/", "\"version\"", null, default);
        fetched.StatusCode.Should().Be(304); fetched.FeedUrl.Should().Be("http://localhost/feed"); requests.Should().HaveCount(2);
    }
    [Fact]
    public async Task OversizedBody_IsBoundedEvenWithoutContentLength()
    {
        var fetcher = new FeedFetcher(new Factory(new Handler(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(new byte[10 * 1024 * 1024 + 1])) })));
        await ((Func<Task>)(async () => await fetcher.FetchAsync("http://localhost/feed", null, null, default))).Should().ThrowAsync<FeedException>().WithMessage("*10MB*");
    }
    [Fact]
    public async Task FastHeaders_StalledBody_UsesTwentySecondDeadline()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var stop = new CancellationTokenSource();
        var serving = Task.Run(async () => { using var socket = await listener.AcceptTcpClientAsync(stop.Token); await using var stream = socket.GetStream(); var buffer = new byte[4096]; _ = await stream.ReadAsync(buffer, stop.Token); await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 100\r\nContent-Type: application/rss+xml\r\n\r\n"), stop.Token); try { await Task.Delay(Timeout.Infinite, stop.Token); } catch (OperationCanceledException) { } });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var fetcher = new FeedFetcher(new Factory(new HttpClientHandler()));
            await ((Func<Task>)(async () => await fetcher.FetchAsync($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/", null, null, default))).Should().ThrowAsync<FeedException>().WithMessage("*超时*");
            watch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(18)).And.BeLessThan(TimeSpan.FromSeconds(27));
        }
        finally { stop.Cancel(); await serving; }
    }
    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        var fetcher = new FeedFetcher(new Factory(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("hello") })));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await ((Func<Task>)(async () => await fetcher.FetchAsync("http://localhost/", null, null, cancel.Token))).Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("latin1", null, "ISO-8859-1", false)]
    [InlineData("utf8", null, "UTF-8", false)]
    [InlineData("utf8", null, "ISO-8859-1", true)]
    [InlineData("utf8", "iso-8859-1", "UTF-8", true)]
    [InlineData("utf16", "iso-8859-1", "UTF-16", true)]
    [InlineData("latin1", "iso-8859-1", "UTF-8", false)]
    public async Task XmlBytes_AreDecodedByBomThenHttpCharsetThenDeclaration(string encodingName, string? httpCharset, string declaration, bool bom)
    {
        var encoding = encodingName == "latin1" ? Encoding.Latin1 : encodingName == "utf16" ? Encoding.Unicode : new UTF8Encoding(bom);
        var xml = $"<?xml version='1.0' encoding='{declaration}'?><rss version='2.0'><channel><title>Café</title><link>https://example.com</link><description>D</description><item><title>Résumé</title><link>https://example.com/article</link><description>Résumé &amp; café</description></item></channel></rss>";
        var bytes = (bom ? encoding.GetPreamble() : []).Concat(encoding.GetBytes(xml)).ToArray();
        var fetcher = new FeedFetcher(new Factory(new Handler(_ => {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentType = new("application/rss+xml") { CharSet = httpCharset }; return response;
        })));
        var fetched = await fetcher.FetchAsync("https://example.com/feed", null, null, default);
        var parser = new FeedParser(new MiniCc.Api.Infra.ImageProxyService(Microsoft.Extensions.Options.Options.Create(new MiniCc.Api.Infra.ImageProxyOptions { SignatureKey = "test" })));
        var feed = parser.Parse(fetched.Body!, fetched.FeedUrl);
        feed.Title.Should().Be("Café"); feed.Items.Single().Title.Should().Be("Résumé"); feed.Items.Single().Summary.Should().Be("Résumé & café");
    }
}
