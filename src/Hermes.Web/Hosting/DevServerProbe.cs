// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Net;
using System.Net.Sockets;

namespace Hermes.Web.Hosting;

/// <summary>
/// Checks whether a frontend dev server is accepting connections.
/// </summary>
internal static class DevServerProbe
{
    /// <summary>
    /// Connects to every address the host resolves to at once, because a dev server may listen on only
    /// one of 127.0.0.1 and ::1, and a refused connect on Windows retries for seconds before failing.
    /// </summary>
    public static bool IsListening(Uri url, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            return IsListeningAsync(url, cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task<bool> IsListeningAsync(Uri url, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(url.Host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(url.Host, cancellationToken);

        var attempts = addresses.Select(address => TryConnectAsync(new IPEndPoint(address, url.Port), cancellationToken)).ToList();
        while (attempts.Count > 0)
        {
            var finished = await Task.WhenAny(attempts);
            if (await finished)
                return true;

            attempts.Remove(finished);
        }

        return false;
    }

    private static async Task<bool> TryConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
    {
        using var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(endPoint, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
