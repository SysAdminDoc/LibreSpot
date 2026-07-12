using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace LibreSpot.Desktop.Services;

/// <summary>
/// SSRF guard for the two user-facing "fetch from an HTTPS URL" surfaces
/// (custom-patch import and shared-profile import). Both can be triggered
/// without an explicit confirmation via the <c>librespot://</c> protocol
/// activation, so a web page could otherwise coerce the app into issuing GETs
/// to loopback / link-local / RFC1918 / cloud-metadata endpoints.
///
/// The guard runs at socket-connect time inside a <see cref="SocketsHttpHandler"/>,
/// so it validates the address the client actually dials — covering redirect
/// hops and DNS rebinding that a scheme-only or host-only check would miss.
/// </summary>
internal static class PrivateNetworkGuard
{
    /// <summary>
    /// Builds an <see cref="HttpMessageHandler"/> that resolves each host and
    /// refuses to connect when any resolved address is a non-public address.
    /// </summary>
    public static SocketsHttpHandler CreateGuardedHandler() => new()
    {
        // Redirects still flow through this handler, so each hop is re-guarded.
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        ConnectCallback = ConnectAsync,
    };

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        IPAddress[] addresses = IPAddress.TryParse(host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

        if (addresses.Length == 0)
        {
            throw new IOException($"'{host}' did not resolve to any address.");
        }

        // Reject if ANY resolved address is disallowed: an attacker who controls
        // DNS could otherwise return one public and one internal record and race
        // the connection to the internal one.
        foreach (var address in addresses)
        {
            if (IsBlocked(address))
            {
                throw new IOException($"'{host}' resolves to a non-public address and cannot be imported from.");
            }
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// True when <paramref name="address"/> is loopback, link-local, private,
    /// unique-local, multicast, or otherwise not a routable public address.
    /// </summary>
    public static bool IsBlocked(IPAddress address)
    {
        // Normalise IPv4-mapped IPv6 (e.g. ::ffff:169.254.169.254) to IPv4 so
        // the range checks below see the real target.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 0                                  // 0.0.0.0/8 "this network"
                || b[0] == 10                                 // 10.0.0.0/8
                || b[0] == 127                                // 127.0.0.0/8
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) // 100.64.0.0/10 CGNAT
                || (b[0] == 169 && b[1] == 254)               // 169.254.0.0/16 link-local + metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)  // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)               // 192.168.0.0/16
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)    // 192.0.0.0/24 IETF protocol
                || b[0] >= 224;                               // 224.0.0.0/4 multicast + 240/4 reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || address.IsIPv6UniqueLocal;
        }

        // Unknown address families are treated as unsafe.
        return true;
    }
}
