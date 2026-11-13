// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Hermes.Web.Hosting;

/// <summary>
/// Checks whether a frontend dev server is accepting connections.
/// </summary>
internal static class DevServerProbe
{
    /// <summary>
    /// Connects to every resolved address at once, since a dev server may listen on only 127.0.0.1 or ::1, and
    /// runs on the calling thread, so a busy thread pool cannot hold up startup.
    /// </summary>
    public static bool IsListening(Uri url, TimeSpan timeout)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        var pending = new List<Socket>();
        try
        {
            foreach (var address in ResolveAddresses(url.Host))
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { Blocking = false };
                pending.Add(socket);
                try
                {
                    socket.Connect(new IPEndPoint(address, url.Port));
                    return true;
                }
                catch (SocketException ex) when (ex.SocketErrorCode is SocketError.WouldBlock or SocketError.InProgress)
                {
                }
                catch (SocketException)
                {
                    pending.Remove(socket);
                    socket.Dispose();
                }
            }

            return WaitForAnyConnection(pending, deadline);
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            foreach (var socket in pending)
                socket.Dispose();
        }
    }

    private static IPAddress[] ResolveAddresses(string host) =>
        IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? [literal] : Dns.GetHostAddresses(host);

    private static bool WaitForAnyConnection(List<Socket> pending, long deadline)
    {
        var waiting = new List<Socket>(pending);
        while (waiting.Count > 0)
        {
            var remaining = Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), deadline);
            if (remaining <= TimeSpan.Zero)
                return false;

            var writable = new List<Socket>(waiting);
            var failed = new List<Socket>(waiting);
            Socket.Select(null, writable, failed, (int)Math.Min(remaining.TotalMicroseconds, int.MaxValue));

            // A failed connect shows up as writable on Unix, with the error in SO_ERROR, and in the error list on Windows.
            if (writable.Exists(socket => !failed.Contains(socket) && socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Error) is 0))
                return true;

            waiting.RemoveAll(socket => writable.Contains(socket) || failed.Contains(socket));
        }

        return false;
    }
}
