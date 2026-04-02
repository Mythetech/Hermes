// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Hermes.Diagnostics;

namespace Hermes.SingleInstance;

/// <summary>
/// Ensures only one instance of an application runs at a time.
/// The first instance acquires a mutex and listens for args from subsequent instances.
/// Subsequent instances detect the mutex, forward their args via named pipe, and should exit.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly bool _isFirstInstance;
    private readonly CancellationTokenSource? _listenerCts;
    private readonly Thread? _listenerThread;
    private volatile bool _disposed;

    /// <summary>
    /// Gets whether this is the first (primary) instance of the application.
    /// </summary>
    public bool IsFirstInstance => _isFirstInstance;

    /// <summary>
    /// Raised when a second instance launches and sends its command-line arguments.
    /// This event fires on a background thread. Use <c>window.Invoke()</c> to marshal to the UI thread.
    /// </summary>
    public event Action<string[]>? SecondInstanceLaunched;

    /// <summary>
    /// Creates a single-instance guard for the given application identifier.
    /// </summary>
    /// <param name="applicationId">
    /// A unique identifier for the application. Must contain only alphanumeric characters, hyphens, underscores, and dots.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when applicationId is null, empty, or contains invalid characters.</exception>
    public SingleInstanceGuard(string applicationId)
    {
        ValidateApplicationId(applicationId);

        var mutexName = $"Hermes_SI_{applicationId}";
        _pipeName = $"Hermes_SI_{applicationId}";

        _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out _isFirstInstance);

        if (_isFirstInstance)
        {
            _listenerCts = new CancellationTokenSource();
            _listenerThread = new Thread(ListenForSecondInstances)
            {
                IsBackground = true,
                Name = $"Hermes-SingleInstance-{applicationId}"
            };
            _listenerThread.Start();
            HermesLogger.Info($"Single instance guard acquired for '{applicationId}'");
        }
        else
        {
            HermesLogger.Info($"Another instance of '{applicationId}' is already running");
        }
    }

    /// <summary>
    /// Sends command-line arguments to the first (primary) instance.
    /// Call this from the second instance before exiting.
    /// </summary>
    /// <param name="args">The command-line arguments to forward.</param>
    /// <returns>True if the arguments were delivered successfully, false otherwise.</returns>
    public bool NotifyFirstInstance(string[] args)
    {
        if (_isFirstInstance)
            return false;

        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 5000);

            var json = JsonSerializer.Serialize(args, SingleInstanceJsonContext.Default.StringArray);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            client.Write(bytes, 0, bytes.Length);
            client.Flush();

            HermesLogger.Info($"Forwarded {args.Length} arg(s) to first instance");
            return true;
        }
        catch (Exception ex)
        {
            HermesLogger.Warning($"Failed to notify first instance: {ex.Message}");
            return false;
        }
    }

    private void ListenForSecondInstances()
    {
        var ct = _listenerCts!.Token;

        while (!_disposed)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None);

                server.WaitForConnectionAsync(ct).GetAwaiter().GetResult();

                if (_disposed) break;

                using var reader = new StreamReader(server, Encoding.UTF8);
                var line = reader.ReadLine();

                if (!string.IsNullOrEmpty(line))
                {
                    var args = JsonSerializer.Deserialize(line, SingleInstanceJsonContext.Default.StringArray);
                    if (args is not null)
                    {
                        HermesLogger.Info($"Received {args.Length} arg(s) from second instance");
                        SecondInstanceLaunched?.Invoke(args);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!_disposed)
                {
                    HermesLogger.Warning($"Single instance listener error: {ex.Message}");
                }
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _listenerCts?.Cancel();

        // Unblock WaitForConnectionAsync by creating a brief client connection
        if (_isFirstInstance)
        {
            try
            {
                using var unblock = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                unblock.Connect(timeout: 1000);
            }
            catch
            {
                // Best effort to unblock the listener
            }
        }

        _listenerThread?.Join(timeout: TimeSpan.FromSeconds(3));
        _listenerCts?.Dispose();

        if (_isFirstInstance)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* Already released or abandoned */ }
        }

        _mutex.Dispose();

        HermesLogger.Info("Single instance guard disposed");
    }

    private static void ValidateApplicationId(string applicationId)
    {
        if (string.IsNullOrWhiteSpace(applicationId))
            throw new ArgumentException("Application ID must not be null or empty.", nameof(applicationId));

        foreach (var c in applicationId)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                throw new ArgumentException(
                    $"Application ID contains invalid character '{c}'. Only alphanumeric characters, hyphens, underscores, and dots are allowed.",
                    nameof(applicationId));
        }
    }
}
