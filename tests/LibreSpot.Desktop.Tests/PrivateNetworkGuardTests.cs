using System.Net;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class PrivateNetworkGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]        // loopback
    [InlineData("0.0.0.0")]          // unspecified / this-network
    [InlineData("10.1.2.3")]         // RFC1918
    [InlineData("172.16.0.1")]       // RFC1918
    [InlineData("172.31.255.254")]   // RFC1918 upper bound
    [InlineData("192.168.1.1")]      // RFC1918
    [InlineData("169.254.169.254")]  // link-local + cloud metadata
    [InlineData("100.64.0.1")]       // CGNAT
    [InlineData("192.0.0.1")]        // IETF protocol assignments
    [InlineData("224.0.0.1")]        // multicast
    [InlineData("240.0.0.1")]        // reserved
    [InlineData("::1")]              // IPv6 loopback
    [InlineData("fe80::1")]          // IPv6 link-local
    [InlineData("fc00::1")]          // IPv6 unique-local
    [InlineData("::ffff:169.254.169.254")] // IPv4-mapped metadata address
    public void IsBlocked_RejectsNonPublicAddresses(string address)
    {
        Assert.True(PrivateNetworkGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("140.82.121.3")]     // github.com range
    [InlineData("2606:4700:4700::1111")] // Cloudflare IPv6
    public void IsBlocked_AllowsPublicAddresses(string address)
    {
        Assert.False(PrivateNetworkGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Fact]
    public void CreateGuardedHandler_DisablesUnboundedRedirects()
    {
        using var handler = PrivateNetworkGuard.CreateGuardedHandler();

        Assert.True(handler.AllowAutoRedirect);
        Assert.Equal(5, handler.MaxAutomaticRedirections);
        Assert.NotNull(handler.ConnectCallback);
    }
}
