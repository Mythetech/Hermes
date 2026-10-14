// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;

namespace Hermes;

/// <summary>
/// Cross-platform access to the system clipboard.
/// </summary>
public static class Clipboard
{
    private static readonly object s_backendLock = new();
    private static readonly object s_subscriptionLock = new();
    private static Subscription[] s_changedHandlers = [];
    private static IClipboardBackend? s_backend;

    /// <summary>
    /// Copies text to the system clipboard, replacing its contents.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when text is null or whitespace.</exception>
    /// <exception cref="PlatformNotSupportedException">Thrown on unsupported platforms.</exception>
    public static void SetText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Write(new HermesDataPackage { Text = text });
    }

    /// <summary>
    /// Gets text from the system clipboard, or null if it holds no text.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown on unsupported platforms.</exception>
    public static string? GetText()
    {
        var text = Read(HermesDataFormats.Text).Text;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Reports which standard formats the clipboard holds, without reading any content.</summary>
    public static HermesDataFormats GetFormats() => GetSupportedBackend().GetFormats();

    /// <summary>Reports whether the clipboard holds the custom format named by <paramref name="mimeType"/>.</summary>
    public static bool HasCustom(string mimeType)
    {
        DataPackageValidator.ThrowIfInvalidCustomType(mimeType, nameof(mimeType));
        return GetSupportedBackend().HasCustom(mimeType);
    }

    /// <summary>
    /// Reads the requested standard formats and the listed custom formats. Missing formats come back null.
    /// </summary>
    public static HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string>? customTypes = null)
    {
        customTypes ??= [];
        foreach (var mimeType in customTypes)
            DataPackageValidator.ThrowIfInvalidCustomType(mimeType, nameof(customTypes));

        return GetSupportedBackend().Read(formats, customTypes);
    }

    /// <summary>Replaces the clipboard contents with every format in <paramref name="package"/>, in one write.</summary>
    public static void Write(HermesDataPackage package)
    {
        DataPackageValidator.ValidateForWrite(package);
        GetSupportedBackend().Write(package);
    }

    /// <summary>Empties the clipboard.</summary>
    public static void Clear() => GetSupportedBackend().Clear();

    /// <summary>
    /// Raised when the clipboard changes, including through this app's own writes. Each handler runs on the
    /// <see cref="SynchronizationContext"/> that was current when it subscribed, or on the platform's thread if none was.
    /// </summary>
    public static event Action? Changed
    {
        add
        {
            if (value is null)
                return;

            var backend = GetSupportedBackend();
            lock (s_subscriptionLock)
            {
                var current = s_changedHandlers;
                if (current.Length == 0)
                    AttachAndStart(backend);

                Volatile.Write(ref s_changedHandlers, [.. current, new Subscription(value, SynchronizationContext.Current)]);
            }
        }
        remove
        {
            if (value is null)
                return;

            lock (s_subscriptionLock)
            {
                var current = s_changedHandlers;
                var index = Array.FindLastIndex(current, entry => entry.Handler == value);
                if (index < 0)
                    return;

                Subscription[] remaining = [.. current[..index], .. current[(index + 1)..]];
                Volatile.Write(ref s_changedHandlers, remaining);
                if (remaining.Length == 0 && Volatile.Read(ref s_backend) is { } backend)
                    StopAndDetach(backend);
            }
        }
    }

    internal static void SetBackendForTesting(IClipboardBackend? backend)
    {
        lock (s_subscriptionLock)
        {
            lock (s_backendLock)
            {
                Volatile.Write(ref s_changedHandlers, []);
                Volatile.Write(ref s_backend, backend);
            }
        }
    }

    private static void AttachAndStart(IClipboardBackend backend)
    {
        backend.Changed += OnBackendChanged;
        try
        {
            backend.StartMonitoring();
        }
        catch
        {
            backend.Changed -= OnBackendChanged;
            throw;
        }
    }

    private static void StopAndDetach(IClipboardBackend backend)
    {
        try
        {
            backend.StopMonitoring();
        }
        catch (Exception ex)
        {
            // Unsubscribing often runs from component disposal, where a throw would hide the real teardown.
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
        finally
        {
            backend.Changed -= OnBackendChanged;
        }
    }

    private static void OnBackendChanged()
    {
        foreach (var (handler, context) in Volatile.Read(ref s_changedHandlers))
        {
            if (context is null)
            {
                InvokeHandler(handler);
                continue;
            }

            try
            {
                context.Post(static state => InvokeHandler((Action)state!), handler);
            }
            catch (Exception ex)
            {
                // A torn-down context can throw from Post; the remaining subscribers must still be notified.
                HermesApplication.RaiseDispatcherUnhandledException(ex);
            }
        }
    }

    // Backends raise Changed from native callbacks, where an escaping exception would end the process.
    private static void InvokeHandler(Action handler)
    {
        try
        {
            handler();
        }
        catch (Exception ex)
        {
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private static IClipboardBackend GetSupportedBackend()
    {
        // Lock-free once created, so a read never waits on a subscribe that is itself waiting on this thread.
        var backend = Volatile.Read(ref s_backend) ?? CreateAndPublishBackend();
        if (!backend.IsSupported)
            throw new PlatformNotSupportedException(backend.UnsupportedReason ?? "Clipboard is not supported on this platform.");

        return backend;
    }

    private static IClipboardBackend CreateAndPublishBackend()
    {
        lock (s_backendLock)
        {
            var backend = s_backend;
            if (backend is null)
            {
                backend = CreateBackend();
                Volatile.Write(ref s_backend, backend);
            }

            return backend;
        }
    }

    private static IClipboardBackend CreateBackend()
    {
#if MACOS
        if (OperatingSystem.IsMacOS())
            return new Platforms.macOS.MacClipboardBackend();
#endif
#if WINDOWS
        if (OperatingSystem.IsWindows())
            return new Platforms.Windows.WindowsClipboardBackend();
#endif
#if LINUX
        if (OperatingSystem.IsLinux())
            return new Platforms.Linux.LinuxClipboardBackend();
#endif
        return new UnsupportedClipboardBackend("Clipboard is not supported on this platform.");
    }

    private readonly record struct Subscription(Action Handler, SynchronizationContext? Context);
}
