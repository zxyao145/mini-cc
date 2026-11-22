using System.Net;
using System.Net.Sockets;

namespace MiniCc.Api.Core.ImageNs.Infrastructure.Download;

public sealed class BlockedAddressException(string host) : Exception($"Image host '{host}' resolved to a private or reserved network address.");

public static class PrivateNetworkGuard
{
    public static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            return IsBlocked(address.MapToIPv4());

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] is 0 or 10 or 127 or >= 224
                || bytes[0] == 100 && bytes[1] is >= 64 and <= 127
                || bytes[0] == 169 && bytes[1] == 254
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return true;

        return address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.IPv6Loopback)
            || (bytes[0] & 0xfe) == 0xfc
            || bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80
            || bytes[0] == 0xff;
    }

    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0)
            throw new SocketException((int)SocketError.HostNotFound);

        if (addresses.Any(IsBlocked))
            throw new BlockedAddressException(host);

        Exception? lastError = null;
        foreach (var resolvedAddress in addresses)
        {
            var address = resolvedAddress.IsIPv4MappedToIPv6 ? resolvedAddress.MapToIPv4() : resolvedAddress;
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                socket.Dispose();
                lastError = exception;
                if (exception is OperationCanceledException)
                    throw;
            }
        }

        throw new HttpRequestException($"Could not connect to image host '{host}'.", lastError);
    }
}
