// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;

namespace Hermes;

/// <summary>
/// System-wide keyboard shortcuts that fire while the app is not focused, including in apps with no window.
/// </summary>
public static class GlobalHotkeys
{
    /// <summary>The most hotkeys one app can register.</summary>
    public const int MaxRegistrations = 128;

    private static readonly Lock s_lock = new();
    private static readonly SemaphoreSlim s_gate = new(1, 1);
    private static readonly Dictionary<string, Registration> s_registrations = new(StringComparer.Ordinal);
    private static Subscription[] s_handlers = [];
    private static IGlobalHotkeyBackend? s_backend;

    /// <summary>
    /// Raised with the id of a registered hotkey when it is pressed. Each handler runs on the
    /// <see cref="SynchronizationContext"/> that was current when it subscribed, or on the platform's thread if none was.
    /// </summary>
    /// <remarks>Handlers must not block on hotkey calls; await them instead.</remarks>
    public static event Action<string>? Activated
    {
        add
        {
            if (value is null)
                return;

            lock (s_lock)
                Volatile.Write(ref s_handlers, [.. s_handlers, new Subscription(value, SynchronizationContext.Current)]);
        }
        remove
        {
            if (value is null)
                return;

            lock (s_lock)
            {
                var index = Array.FindLastIndex(s_handlers, s => s.Handler == value);
                if (index >= 0)
                    Volatile.Write(ref s_handlers, [.. s_handlers[..index], .. s_handlers[(index + 1)..]]);
            }
        }
    }

    /// <summary>
    /// Registers a hotkey under an app-defined id, in the menu accelerator syntax such as "Ctrl+Shift+K".
    /// Ctrl means Command on macOS, and Meta means the Control key there.
    /// </summary>
    /// <param name="id">An app-defined id that <see cref="Activated"/> reports.</param>
    /// <param name="accelerator">One key and at least one modifier.</param>
    /// <param name="description">Shown in desktop shortcut settings where the platform has them; defaults to the id.</param>
    /// <exception cref="ArgumentException">The id is blank, or the accelerator is invalid, has no modifier, uses an unsupported key, or is reserved.</exception>
    /// <remarks>Await it rather than blocking on it: completing it can need the app's message loop, so a wait on the UI thread, before the loop runs, or in an <see cref="Activated"/> handler may never return.</remarks>
    public static async Task<HermesHotkeyResult> RegisterAsync(string id, string accelerator, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var parsed = HotkeyAccelerator.Parse(accelerator, nameof(accelerator));
        var canonical = HotkeyAccelerator.ToCanonicalString(parsed);

        var backend = GetBackend();
        if (!backend.IsSupported)
            return new HermesHotkeyResult(HermesHotkeyStatus.Unsupported, null, backend.UnsupportedReason);

        // Registrations change one at a time, so an unregister can never cross a registration of the same id.
        await s_gate.WaitAsync();
        try
        {
            lock (s_lock)
            {
                if (s_registrations.ContainsKey(id))
                    return new HermesHotkeyResult(HermesHotkeyStatus.Conflict, null, $"A hotkey with id '{id}' is already registered.");

                if (s_registrations.Values.Any(r => r.Accelerator == canonical))
                    return new HermesHotkeyResult(HermesHotkeyStatus.Conflict, null, $"{canonical} is already registered in this app.");

                if (s_registrations.Count >= MaxRegistrations)
                    return new HermesHotkeyResult(HermesHotkeyStatus.LimitExceeded, null, $"An app can register at most {MaxRegistrations} hotkeys.");

                s_registrations[id] = new Registration(canonical);
            }

            HermesHotkeyResult result;
            try
            {
                result = await backend.RegisterAsync(id, parsed, description ?? id);
            }
            catch
            {
                Forget(id);
                throw;
            }

            if (result.Status != HermesHotkeyStatus.Registered)
                Forget(id);

            return result;
        }
        finally
        {
            s_gate.Release();
        }
    }

    /// <summary>
    /// Unregisters a hotkey. Unknown ids are ignored. Presses not yet delivered when this completes are dropped.
    /// </summary>
    /// <remarks>Hotkey calls wait for a registration still in progress, such as one showing a desktop consent dialog, which never finishes once the message loop has ended.</remarks>
    public static async Task UnregisterAsync(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await s_gate.WaitAsync();
        try
        {
            Registration? removed;
            lock (s_lock)
            {
                s_registrations.Remove(id, out removed);
            }

            if (removed is not null && Volatile.Read(ref s_backend) is { } backend)
            {
                try
                {
                    await backend.UnregisterAsync(id);
                }
                catch
                {
                    lock (s_lock)
                        s_registrations[id] = removed;
                    throw;
                }
            }
        }
        finally
        {
            s_gate.Release();
        }
    }

    internal static void SetBackendForTesting(IGlobalHotkeyBackend? backend)
    {
        lock (s_lock)
        {
            if (s_backend is not null)
                s_backend.Activated -= OnBackendActivated;

            s_registrations.Clear();
            if (backend is not null)
                backend.Activated += OnBackendActivated;

            Volatile.Write(ref s_backend, backend);
        }
    }

    private static void Forget(string id)
    {
        lock (s_lock)
            s_registrations.Remove(id);
    }

    private static void OnBackendActivated(string id)
    {
        Registration? registration;
        lock (s_lock)
        {
            if (!s_registrations.TryGetValue(id, out registration))
                return;
        }

        foreach (var (handler, context) in Volatile.Read(ref s_handlers))
        {
            if (context is null)
            {
                InvokeHandler(handler, id, registration);
                continue;
            }

            try
            {
                context.Post(static state =>
                {
                    var (h, i, r) = ((Action<string>, string, Registration))state!;
                    InvokeHandler(h, i, r);
                }, (handler, id, registration));
            }
            catch (Exception ex)
            {
                // A torn-down context can throw from Post; the remaining subscribers must still be notified.
                HermesApplication.RaiseDispatcherUnhandledException(ex);
            }
        }
    }

    // Backends raise Activated from native callbacks, where an escaping exception would end the process.
    private static void InvokeHandler(Action<string> handler, string id, Registration registration)
    {
        lock (s_lock)
        {
            if (!s_registrations.TryGetValue(id, out var current) || !ReferenceEquals(current, registration))
                return;
        }

        try
        {
            handler(id);
        }
        catch (Exception ex)
        {
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private static IGlobalHotkeyBackend GetBackend()
    {
        if (Volatile.Read(ref s_backend) is { } existing)
            return existing;

        lock (s_lock)
        {
            if (s_backend is { } created)
                return created;

            var backend = CreateBackend();
            backend.Activated += OnBackendActivated;
            Volatile.Write(ref s_backend, backend);
            return backend;
        }
    }

    // Runs under s_lock, so a backend constructor must not wait on the UI thread.
    private static IGlobalHotkeyBackend CreateBackend()
    {
#if MACOS
        if (OperatingSystem.IsMacOS())
            return Platforms.macOS.MacGlobalHotkeyBackend.Create();
#endif
#if WINDOWS
        if (OperatingSystem.IsWindows())
            return new Platforms.Windows.WindowsGlobalHotkeyBackend();
#endif
#if LINUX
        if (OperatingSystem.IsLinux())
        {
            try
            {
                return new Platforms.Linux.LinuxGlobalHotkeyBackend();
            }
            catch (DllNotFoundException ex)
            {
                return new UnsupportedGlobalHotkeyBackend($"The Hermes native library was not found: {ex.Message}");
            }
            catch (EntryPointNotFoundException ex)
            {
                return new UnsupportedGlobalHotkeyBackend($"The Hermes native library is out of date: {ex.Message}");
            }
        }
#endif
        return new UnsupportedGlobalHotkeyBackend("Global hotkeys are not supported on this platform.");
    }

    private sealed class Registration(string accelerator)
    {
        public string Accelerator { get; } = accelerator;
    }

    private readonly record struct Subscription(Action<string> Handler, SynchronizationContext? Context);
}
