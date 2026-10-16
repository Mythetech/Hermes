// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Menu;

namespace Hermes.Platforms.macOS;

/// <summary>
/// Global hotkeys through Carbon's RegisterEventHotKey, which needs no Accessibility permission.
/// Registration runs on the main thread, and presses arrive there.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacGlobalHotkeyBackend : IGlobalHotkeyBackend
{
    internal const int NoErr = 0;
    internal const int EventHotKeyExistsErr = -9878;
    internal const int EventInternalErr = -9868;

    // Static so the delegate outlives every hotkey the process registers.
    private static readonly MacNativeDelegates.HotkeyPressedCallback s_pressedCallback = OnNativePressed;
    private static MacGlobalHotkeyBackend? s_current;

    private readonly Lock _lock = new();
    private readonly Dictionary<uint, string> _idsByNumber = [];
    private readonly Dictionary<string, uint> _numbersById = new(StringComparer.Ordinal);
    private uint _lastNumber;

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    public event Action<string>? Activated;

    /// <summary>Connects to the native library, or returns an Unsupported backend when it lacks hotkeys.</summary>
    internal static IGlobalHotkeyBackend Create()
    {
        try
        {
            MacNative.HotkeysSetCallback(Marshal.GetFunctionPointerForDelegate(s_pressedCallback));
        }
        catch (DllNotFoundException ex)
        {
            return new UnsupportedGlobalHotkeyBackend($"The Hermes native library was not found: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            return new UnsupportedGlobalHotkeyBackend($"The Hermes native library is out of date: {ex.Message}");
        }

        var backend = new MacGlobalHotkeyBackend();
        Volatile.Write(ref s_current, backend);
        return backend;
    }

    public Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (HotkeyKeyMap.MacKeyCode(accelerator.Key) is not { } keyCode)
            return Task.FromResult(new HermesHotkeyResult(HermesHotkeyStatus.Unsupported, null, $"Mac keyboards have no {accelerator.Key} key."));

        uint number;
        lock (_lock)
        {
            // Mapped before the native call, so a press that arrives as registration completes finds its id.
            number = ++_lastNumber;
            _idsByNumber[number] = id;
        }

        var status = MacNative.HotkeysRegister(number, keyCode, HotkeyKeyMap.MacModifiers(accelerator));

        lock (_lock)
        {
            if (status == NoErr)
                _numbersById[id] = number;
            else
                _idsByNumber.Remove(number);
        }

        return Task.FromResult(ToResult(status, accelerator));
    }

    public Task UnregisterAsync(string id)
    {
        uint number;
        lock (_lock)
        {
            if (!_numbersById.Remove(id, out number))
                return Task.CompletedTask;

            _idsByNumber.Remove(number);
        }

        MacNative.HotkeysUnregister(number);
        return Task.CompletedTask;
    }

    /// <summary>Maps an OSStatus from RegisterEventHotKey to a result.</summary>
    internal static HermesHotkeyResult ToResult(int status, Accelerator accelerator)
    {
        var canonical = HotkeyAccelerator.ToCanonicalString(accelerator);
        return status switch
        {
            NoErr => new HermesHotkeyResult(HermesHotkeyStatus.Registered, canonical, null),
            EventHotKeyExistsErr => new HermesHotkeyResult(HermesHotkeyStatus.Conflict, null,
                $"{canonical} is already taken by another app or a system shortcut."),

            // macOS 15.0 and 15.1 refuse hotkeys whose only modifiers are Option or Option+Shift this way.
            EventInternalErr when IsOptionOnly(accelerator) => new HermesHotkeyResult(HermesHotkeyStatus.Unsupported, null,
                "This version of macOS does not allow global hotkeys whose only modifiers are Option or Option+Shift."),
            _ => new HermesHotkeyResult(HermesHotkeyStatus.Failed, null, $"macOS could not register {canonical} (OSStatus {status})."),
        };
    }

    internal void RaisePressed(uint hotkeyNumber)
    {
        string? id;
        lock (_lock)
            _idsByNumber.TryGetValue(hotkeyNumber, out id);

        if (id is not null)
            Activated?.Invoke(id);
    }

    private static bool IsOptionOnly(Accelerator accelerator) =>
        accelerator.Alt && !accelerator.Control && !accelerator.Meta;

    private static void OnNativePressed(uint hotkeyNumber) => Volatile.Read(ref s_current)?.RaisePressed(hotkeyNumber);
}
