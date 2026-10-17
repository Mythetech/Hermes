// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Menu;

namespace Hermes.Platforms.Linux;

/// <summary>
/// Linux global hotkeys: X11 key grabs, or the GlobalShortcuts portal on Wayland. The native side does all
/// of its work on the GTK thread and reports registrations and presses by number; a registration made
/// before the first window completes once the GTK main loop runs.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxGlobalHotkeyBackend : IGlobalHotkeyBackend
{
    // Static so the delegates outlive every native callback, which can arrive at any time before the process exits.
    private static readonly LinuxNativeDelegates.HotkeyActivatedCallback s_activatedCallback = OnNativeActivated;
    private static readonly LinuxNativeDelegates.HotkeyRegisteredCallback s_registeredCallback = OnNativeRegistered;
    private static LinuxGlobalHotkeyBackend? s_current;

    private const int NativeStatusFailed = 4;

    private readonly Action<int, string, string, string, uint, string> _register;
    private readonly Action<int> _unregister;
    private readonly Lock _lock = new();
    private readonly NativeHotkeyTable _table = new();

    public LinuxGlobalHotkeyBackend()
        : this(LinuxNative.HotkeysRegister, LinuxNative.HotkeysUnregister)
    {
        LinuxNative.HotkeysStart(
            Marshal.GetFunctionPointerForDelegate(s_activatedCallback),
            Marshal.GetFunctionPointerForDelegate(s_registeredCallback));
        Volatile.Write(ref s_current, this);
    }

    internal LinuxGlobalHotkeyBackend(Action<int, string, string, string, uint, string> register, Action<int> unregister)
    {
        _register = register;
        _unregister = unregister;
    }

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    public event Action<string>? Activated;

    public async Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description, CancellationToken ct)
    {
        var keysym = HotkeyKeyMap.X11KeysymName(accelerator.Key)
            ?? throw new ArgumentException($"'{accelerator.Key}' has no X11 keysym.", nameof(accelerator));
        ct.ThrowIfCancellationRequested();
        var trigger = PortalTrigger.From(accelerator);

        int number;
        Task<HermesHotkeyResult> completion;
        // Numbering and queueing happen together, so an unregister can never reach the GTK thread first.
        lock (_lock)
        {
            number = _table.Begin(id, HotkeyAccelerator.ToCanonicalString(accelerator), out completion);
            _register(number, id, description, keysym, HotkeyKeyMap.X11Modifiers(accelerator), trigger);
        }

        // Completing here frees the caller without the GTK loop; the queued unregister also withdraws a Wayland consent dialog.
        using var registration = ct.Register(() => CancelPending(number));
        return await completion;
    }

    private void CancelPending(int number)
    {
        lock (_lock)
        {
            if (_table.Complete(number, NativeStatusFailed, null, "The registration was canceled."))
                _unregister(number);
        }
    }

    // Completes at once: the table stops presses now, and the GTK thread releases the grab in queue order.
    public Task UnregisterAsync(string id)
    {
        lock (_lock)
        {
            if (_table.Remove(id) is { } number)
                _unregister(number);
        }

        return Task.CompletedTask;
    }

    internal void OnActivated(int number)
    {
        try
        {
            if (_table.IdFor(number) is { } id)
                Activated?.Invoke(id);
        }
        catch (Exception ex)
        {
            // Native callbacks run inside the GTK main loop, which an exception must never unwind.
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    internal void OnRegistered(int number, int status, string? boundTrigger, string? reason)
    {
        try
        {
            _table.Complete(number, status, boundTrigger, reason);
        }
        catch (Exception ex)
        {
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private static void OnNativeActivated(int number) => Volatile.Read(ref s_current)?.OnActivated(number);

    private static void OnNativeRegistered(int number, int status, IntPtr boundTriggerPtr, IntPtr reasonPtr) =>
        Volatile.Read(ref s_current)?.OnRegistered(
            number, status, Marshal.PtrToStringUTF8(boundTriggerPtr), Marshal.PtrToStringUTF8(reasonPtr));
}
