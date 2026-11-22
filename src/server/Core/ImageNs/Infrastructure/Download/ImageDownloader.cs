using System.Collections.Concurrent;
using System.Net;
using MiniCc.Api.Core.ImageNs.Application;
using MiniCc.Api.Core.ImageNs.Domain;

namespace MiniCc.Api.Core.ImageNs.Infrastructure.Download;

public sealed class ImageDownloader(IHttpClientFactory clients, TimeProvider? timeProvider = null) : IImageDownloader
{
    public const int MaxBytes = 20 * 1024 * 1024;

    private const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 11_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.4280.141 Safari/537.36";
    private const string Accept = "image/avif,image/webp,image/apng,image/*,*/*;q=0.8";
    private static readonly ConcurrentDictionary<string, HostGate> HostConcurrency = new(StringComparer.OrdinalIgnoreCase);

    public static SocketsHttpHandler CreateHandler(bool allowPrivateNetworks) => new()
    {
        UseProxy = false,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        MaxConnectionsPerServer = 2,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectCallback = allowPrivateNetworks ? null : PrivateNetworkGuard.ConnectAsync
    };

    public async Task<DownloadResult> DownloadAsync(string url, string articleUrl, CancellationToken cancellationToken)
    {
        if (!ImageHtml.TryGetHttpUri(url, out var uri))
            return new(DownloadOutcome.Permanent, Reason: "invalidUrl");

        var host = uri.IdnHost;
        var hostGate = AcquireHostGate(host);
        var acquiredHostPermit = false;
        try
        {
            await hostGate.Semaphore.WaitAsync(cancellationToken);
            acquiredHostPermit = true;

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            return await DownloadWithRefererRetryAsync(uri, articleUrl, deadline.Token);
        }
        catch (BlockedAddressException)
        {
            return new(DownloadOutcome.Permanent, Reason: "blockedAddress");
        }
        catch (HttpRequestException exception) when (ContainsBlockedAddress(exception))
        {
            return new(DownloadOutcome.Permanent, Reason: "blockedAddress");
        }
        catch (ImageTooLargeException)
        {
            return new(DownloadOutcome.Permanent, Reason: "tooLarge");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(DownloadOutcome.Transient, Reason: "timeout");
        }
        catch (HttpRequestException)
        {
            return new(DownloadOutcome.Transient, Reason: "network");
        }
        catch (IOException)
        {
            return new(DownloadOutcome.Transient, Reason: "network");
        }
        finally
        {
            if (acquiredHostPermit)
                hostGate.Semaphore.Release();
            ReleaseHostGate(host, hostGate);
        }
    }

    private async Task<DownloadResult> DownloadWithRefererRetryAsync(Uri uri, string articleUrl, CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient("images");
        var response = await SendAsync(client, uri, referer: null, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            && ImageHtml.TryGetHttpUri(articleUrl, out var refererUri))
        {
            response.Dispose();
            response = await SendAsync(client, uri, refererUri, cancellationToken);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(DownloadOutcome.Permanent, Reason: "forbidden");

            if (!response.IsSuccessStatusCode)
                return ClassifyStatus(response);

            var content = await ReadContentAsync(response.Content, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var ext = ImageSniffer.Detect(content);
            return ext is null
                ? new(DownloadOutcome.Permanent, Reason: "unsupportedType")
                : new(DownloadOutcome.Success, content, ext);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, Uri uri, Uri? referer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.ParseAdd(Accept);
        if (referer is not null)
            request.Headers.Referrer = referer;
        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static async Task<byte[]> ReadContentAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxBytes)
            throw new ImageTooLargeException();

        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream(content.Headers.ContentLength is { } length ? (int)length : 0);
        var buffer = new byte[8192];
        while (true)
        {
            var remaining = MaxBytes + 1 - (int)output.Length;
            var read = await input.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
                return output.ToArray();

            if (output.Length + read > MaxBytes)
                throw new ImageTooLargeException();

            output.Write(buffer, 0, read);
        }
    }

    private static DownloadResult ClassifyStatus(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return new(DownloadOutcome.Permanent, Reason: "notFound");

        if (statusCode is >= 400 and < 500 && response.StatusCode is not (HttpStatusCode.RequestTimeout or (HttpStatusCode)429))
            return new(DownloadOutcome.Permanent, Reason: $"http{statusCode}");

        if (statusCode is 408 or 429 or >= 500)
        {
            var retryAfter = response.Headers.RetryAfter?.Date;
            if (retryAfter is null && response.Headers.RetryAfter?.Delta is { } delta)
                retryAfter = DateTimeOffset.UtcNow + delta;
            return new(DownloadOutcome.Transient, Reason: $"http{statusCode}", RetryAfter: retryAfter);
        }

        return new(DownloadOutcome.Permanent, Reason: $"http{statusCode}");
    }

    private static bool ContainsBlockedAddress(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is BlockedAddressException)
                return true;
        }
        return false;
    }

    private static HostGate AcquireHostGate(string host)
    {
        while (true)
        {
            var gate = HostConcurrency.GetOrAdd(host, static _ => new HostGate());
            lock (gate)
            {
                if (!HostConcurrency.TryGetValue(host, out var current) || !ReferenceEquals(current, gate))
                    continue;

                gate.References++;
                return gate;
            }
        }
    }

    private static void ReleaseHostGate(string host, HostGate gate)
    {
        lock (gate)
        {
            gate.References--;
            if (gate.References != 0)
                return;

            var entries = (ICollection<KeyValuePair<string, HostGate>>)HostConcurrency;
            if (HostConcurrency.TryGetValue(host, out var current) && ReferenceEquals(current, gate)
                && entries.Remove(new KeyValuePair<string, HostGate>(host, gate)))
            {
                gate.Semaphore.Dispose();
            }
        }
    }

    private sealed class HostGate
    {
        public SemaphoreSlim Semaphore { get; } = new(2, 2);
        public int References { get; set; }
    }

    private sealed class ImageTooLargeException : Exception { }
}
