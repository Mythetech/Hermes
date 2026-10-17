// Copyright (c) Mythetech. Licensed under the MIT License.
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Menu;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

/// <summary>
/// Registers global hotkeys from a thread of its own with a message-only window, so they work without any Hermes window.
/// The thread starts with the first registration and ends when the last one is unregistered.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsGlobalHotkeyBackend : IGlobalHotkeyBackend
{
    internal const string ThreadName = "Hermes Global Hotkeys";
    private const int ErrorHotkeyAlreadyRegistered = 1409;
    private const int MaxHotkeyNumber = 0xBFFF;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Registration> _registrations = new(StringComparer.Ordinal);
    private HotkeyThread? _thread;
    private int _lastNumber;

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    public event Action<string>? Activated;

    /// <summary>Completes when the running hotkey thread has ended; null while no thread runs.</summary>
    internal Task? ThreadExit
    {
        get
        {
            lock (_lock)
                return _thread?.Exit;
        }
    }

    internal static HOT_KEY_MODIFIERS NativeModifiers(Accelerator accelerator) =>
        (HOT_KEY_MODIFIERS)HotkeyKeyMap.WindowsModifiers(accelerator) | HOT_KEY_MODIFIERS.MOD_NOREPEAT;

    public Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (HotkeyKeyMap.WindowsVirtualKey(accelerator.Key) is not { } virtualKey)
            return Task.FromResult(new HermesHotkeyResult(HermesHotkeyStatus.Unsupported, null, $"{accelerator.Key} has no Windows key code."));

        var canonical = HotkeyAccelerator.ToCanonicalString(accelerator);
        var modifiers = NativeModifiers(accelerator);
        var completion = new TaskCompletionSource<HermesHotkeyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            // The older hotkey is released before a supported key is registered again.
            if (_registrations.Remove(id, out var previous))
                PostUnregister(previous, null);

            var thread = _thread ??= HotkeyThread.Start(this);
            var number = NextNumber();
            thread.Active++;
            _registrations[id] = new Registration(thread, number);
            thread.Post(() => thread.Register(id, number, modifiers, virtualKey, canonical, completion));
        }

        return completion.Task;
    }

    public Task UnregisterAsync(string id)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            if (!_registrations.Remove(id, out var registration))
                return Task.CompletedTask;

            PostUnregister(registration, completion);
        }

        return completion.Task;
    }

    /// <summary>Posts the message Windows sends when the hotkey registered under this id is pressed.</summary>
    internal bool SimulatePressForTesting(string id)
    {
        lock (_lock)
            return _registrations.TryGetValue(id, out var registration) && registration.Thread.PostPress(registration.Number);
    }

    // Caller holds the lock.
    private static void PostUnregister(Registration registration, TaskCompletionSource? completion)
    {
        var (thread, number) = registration;
        thread.Post(() => thread.Unregister(number, completion));
    }

    // Numbers count up, so a press queued for an unregistered hotkey cannot report a newer one before the range wraps.
    private int NextNumber()
    {
        do
            _lastNumber = _lastNumber >= MaxHotkeyNumber ? 1 : _lastNumber + 1;
        while (_registrations.Values.Any(r => r.Number == _lastNumber));

        return _lastNumber;
    }

    private void RaiseActivated(string id)
    {
        try
        {
            Activated?.Invoke(id);
        }
        catch (Exception ex)
        {
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    // Called on the hotkey thread for a registration Windows refused or that ended.
    private void Release(HotkeyThread thread, string? refusedId, int number)
    {
        lock (_lock)
        {
            if (refusedId is not null
                && _registrations.TryGetValue(refusedId, out var current)
                && current == new Registration(thread, number))
            {
                _registrations.Remove(refusedId);
            }

            if (--thread.Active > 0)
                return;

            if (_thread == thread)
                _thread = null;
            thread.Stopping = true;
        }
    }

    // Called on a hotkey thread whose window could not be created; nothing is posted to it afterwards.
    private void Abandon(HotkeyThread thread)
    {
        lock (_lock)
        {
            if (_thread == thread)
                _thread = null;

            foreach (var (id, registration) in _registrations.ToArray())
            {
                if (registration.Thread == thread)
                    _registrations.Remove(id);
            }
        }
    }

    private readonly record struct Registration(HotkeyThread Thread, int Number);

    private sealed class HotkeyThread
    {
        private const string WindowClassName = "HermesGlobalHotkeys";
        private const uint WmWork = PInvoke.WM_USER + 1;

        // HWND_MESSAGE is a macro cast ((HWND)-3) in winuser.h, which CsWin32 cannot emit as a constant.
        private static readonly HWND s_messageOnlyParent = new(-3);
        private static readonly WNDPROC s_wndProc = (hwnd, msg, wParam, lParam) => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        private static readonly Lock s_classLock = new();
        private static bool s_classRegistered;
        private static HINSTANCE s_hInstance;

        private readonly WindowsGlobalHotkeyBackend _owner;
        private readonly Queue<Action> _work = new();
        private readonly Dictionary<int, string> _registered = new();
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private HWND _hwnd;
        private Exception? _startError;

        private HotkeyThread(WindowsGlobalHotkeyBackend owner) => _owner = owner;

        /// <summary>Registrations queued to or held by this thread; guarded by the owner's lock.</summary>
        public int Active { get; set; }

        /// <summary>Set on this thread once nothing is registered or queued, so it ends after its queue drains.</summary>
        public bool Stopping { get; set; }

        public Task Exit => _exit.Task;

        public static HotkeyThread Start(WindowsGlobalHotkeyBackend owner)
        {
            var hotkeyThread = new HotkeyThread(owner);
            new Thread(hotkeyThread.Run) { IsBackground = true, Name = ThreadName }.Start();
            return hotkeyThread;
        }

        // Caller holds the owner's lock; work queued before the window exists runs once it does.
        public void Post(Action work)
        {
            _work.Enqueue(work);
            if (!_hwnd.IsNull)
                PInvoke.PostMessage(_hwnd, WmWork, 0, 0);
        }

        // Caller holds the owner's lock.
        public bool PostPress(int number) =>
            !_hwnd.IsNull && PInvoke.PostMessage(_hwnd, PInvoke.WM_HOTKEY, (nuint)number, 0);

        public void Register(string id, int number, HOT_KEY_MODIFIERS modifiers, ushort virtualKey, string canonical,
            TaskCompletionSource<HermesHotkeyResult> completion)
        {
            if (_startError is not null)
            {
                completion.TrySetResult(new HermesHotkeyResult(HermesHotkeyStatus.Failed, null,
                    $"The global hotkey window could not be created: {_startError.Message}"));
                return;
            }

            if (PInvoke.RegisterHotKey(_hwnd, number, modifiers, virtualKey))
            {
                _registered[number] = id;
                completion.TrySetResult(new HermesHotkeyResult(HermesHotkeyStatus.Registered, canonical, null));
                return;
            }

            var error = Marshal.GetLastWin32Error();
            _owner.Release(this, id, number);
            completion.TrySetResult(error == ErrorHotkeyAlreadyRegistered
                ? new HermesHotkeyResult(HermesHotkeyStatus.Conflict, null, $"Another app has already registered {canonical}.")
                : new HermesHotkeyResult(HermesHotkeyStatus.Failed, null,
                    $"Windows could not register {canonical}: {new Win32Exception(error).Message} (error {error})."));
        }

        public void Unregister(int number, TaskCompletionSource? completion)
        {
            if (_registered.Remove(number))
            {
                PInvoke.UnregisterHotKey(_hwnd, number);
                _owner.Release(this, null, number);
            }

            completion?.TrySetResult();
        }

        private unsafe void Run()
        {
            HWND hwnd;
            try
            {
                EnsureWindowClassRegistered();
                fixed (char* className = WindowClassName)
                {
                    hwnd = PInvoke.CreateWindowEx(0, className, className, 0, 0, 0, 0, 0,
                        s_messageOnlyParent, HMENU.Null, s_hInstance, null);
                }

                if (hwnd.IsNull)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create the global hotkey window");
            }
            catch (Exception ex)
            {
                _startError = ex;
                _owner.Abandon(this);
                DrainWork();
                _exit.TrySetResult();
                return;
            }

            lock (_owner._lock)
                _hwnd = hwnd;

            if (!DrainWork())
            {
                while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
                {
                    if (msg.hwnd == hwnd && msg.message == WmWork)
                    {
                        if (DrainWork())
                            break;
                        continue;
                    }

                    if (msg.hwnd == hwnd && msg.message == PInvoke.WM_HOTKEY)
                    {
                        if (_registered.TryGetValue((int)msg.wParam.Value, out var id))
                            _owner.RaiseActivated(id);
                        continue;
                    }

                    PInvoke.TranslateMessage(in msg);
                    PInvoke.DispatchMessage(in msg);
                }
            }

            PInvoke.DestroyWindow(hwnd);
            _exit.TrySetResult();
        }

        // Returns true once this thread should end.
        private bool DrainWork()
        {
            while (true)
            {
                Action? work;
                lock (_owner._lock)
                {
                    if (!_work.TryDequeue(out work))
                        return Stopping;
                }

                work();
            }
        }

        private static unsafe void EnsureWindowClassRegistered()
        {
            lock (s_classLock)
            {
                if (s_classRegistered)
                    return;

                s_hInstance = PInvoke.GetModuleHandle((PCWSTR)null);
                fixed (char* className = WindowClassName)
                {
                    var wcx = new WNDCLASSEXW
                    {
                        cbSize = (uint)sizeof(WNDCLASSEXW),
                        lpfnWndProc = s_wndProc,
                        hInstance = s_hInstance,
                        lpszClassName = className,
                    };

                    if (PInvoke.RegisterClassEx(in wcx) == 0)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register the global hotkey window class");
                }

                s_classRegistered = true;
            }
        }
    }
}
