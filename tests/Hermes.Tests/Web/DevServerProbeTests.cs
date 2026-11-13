// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Hermes.Web.Hosting;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class DevServerProbeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);

    [Fact]
    public void IsListening_TrueForAListener()
    {
        using var listener = Listen(IPAddress.Loopback, out var port);

        Assert.True(DevServerProbe.IsListening(new Uri($"http://127.0.0.1:{port}"), Timeout));
    }

    [Fact]
    public void IsListening_FalseAfterStop_WithinTimeout()
    {
        int port;
        using (Listen(IPAddress.Loopback, out port))
        {
        }

        var stopwatch = Stopwatch.StartNew();
        var listening = DevServerProbe.IsListening(new Uri($"http://127.0.0.1:{port}"), Timeout);
        stopwatch.Stop();

        Assert.False(listening);
        Assert.True(stopwatch.Elapsed < Timeout + TimeSpan.FromSeconds(1), $"The probe took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void IsListening_LocalhostFindsAnIpv4OnlyListener()
    {
        using var listener = Listen(IPAddress.Loopback, out var port);

        Assert.True(DevServerProbe.IsListening(new Uri($"http://localhost:{port}"), Timeout));
    }

    [Fact]
    public void IsListening_LocalhostFindsAnIpv6OnlyListener()
    {
        if (!Socket.OSSupportsIPv6)
            return;

        using var listener = Listen(IPAddress.IPv6Loopback, out var port);

        Assert.True(DevServerProbe.IsListening(new Uri($"http://localhost:{port}"), Timeout));
    }

    private static TcpListener Listen(IPAddress address, out int port)
    {
        var listener = new TcpListener(address, 0);
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            listener.Server.DualMode = false;
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }
}
