using System.Net;
using System.Net.Sockets;
using System.Text;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Infrastructure.Download;

namespace MiniCc.Api.Test.Images;

public class PrivateNetworkGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.31.255.1")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:192.168.0.1")]
    public void IsBlocked_PrivateAddresses(string value)
    {
        PrivateNetworkGuard.IsBlocked(IPAddress.Parse(value)).Should().BeTrue();
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700::1111")]
    public void IsBlocked_PublicAddresses(string value)
    {
        PrivateNetworkGuard.IsBlocked(IPAddress.Parse(value)).Should().BeFalse();
    }

    [Fact]
    public void Handler_DisablesEnvironmentProxy()
    {
        using var handler = ImageDownloader.CreateHandler(allowPrivateNetworks: false);

        handler.UseProxy.Should().BeFalse();
    }

    [Fact]
    public async Task Client_LoopbackServer_IsBlockedUnlessAllowed()
    {
        using var server = new LoopbackImageServer();
        var factory = new HttpClientFactory(allowPrivateNetworks: false);
        var downloader = new ImageDownloader(factory);

        var blocked = await downloader.DownloadAsync(server.Url, "https://article.example/", CancellationToken.None);

        blocked.Outcome.Should().Be(DownloadOutcome.Permanent);
        blocked.Reason.Should().Be("blockedAddress");

        using var allowedFactory = new HttpClientFactory(allowPrivateNetworks: true);
        var allowedDownloader = new ImageDownloader(allowedFactory);
        var allowed = await allowedDownloader.DownloadAsync(server.Url, "https://article.example/", CancellationToken.None);

        allowed.Outcome.Should().Be(DownloadOutcome.Success);
        allowed.Content.Should().Equal(LoopbackImageServer.Png);
    }

    private sealed class HttpClientFactory(bool allowPrivateNetworks) : IHttpClientFactory, IDisposable
    {
        private readonly HttpClient _client = new(ImageDownloader.CreateHandler(allowPrivateNetworks));

        public HttpClient CreateClient(string name) => _client;

        public void Dispose() => _client.Dispose();
    }

    private sealed class LoopbackImageServer : IDisposable
    {
        public static byte[] Png { get; } = [137, 80, 78, 71, 13, 10, 26, 10];

        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _run;

        public LoopbackImageServer()
        {
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/image.png";
            _run = ServeAsync();
        }

        public string Url { get; }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Stop();
            try { _run.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        }

        private async Task ServeAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            await using var stream = client.GetStream();
            var request = new byte[4096];
            var length = 0;
            while (length < request.Length)
            {
                var read = await stream.ReadAsync(request.AsMemory(length), _stopping.Token);
                if (read == 0) break;
                length += read;
                if (Encoding.ASCII.GetString(request, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
            }

            var headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {Png.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers, _stopping.Token);
            await stream.WriteAsync(Png, _stopping.Token);
        }
    }
}
