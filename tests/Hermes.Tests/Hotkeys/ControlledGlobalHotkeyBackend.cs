// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Menu;

namespace Hermes.Tests.Hotkeys;

/// <summary>A backend whose register and unregister calls can be held open or made to throw, recording call order.</summary>
internal sealed class ControlledGlobalHotkeyBackend : IGlobalHotkeyBackend
{
    private readonly object _lock = new();
    private readonly List<string> _calls = [];

    public TaskCompletionSource? HoldRegister { get; set; }

    public HermesHotkeyResult? RegisterResult { get; set; }

    public TaskCompletionSource? HoldUnregister { get; set; }

    public TaskCompletionSource UnregisterEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Exception? RegisterException { get; set; }

    public Exception? UnregisterException { get; set; }

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_lock)
                return [.. _calls];
        }
    }

    public event Action<string>? Activated;

    public async Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description, CancellationToken ct)
    {
        Record("register:" + id);
        if (HoldRegister is { } hold)
            await hold.Task;

        if (RegisterException is { } ex)
            throw ex;

        return RegisterResult ?? new HermesHotkeyResult(HermesHotkeyStatus.Registered, HotkeyAccelerator.ToCanonicalString(accelerator), null);
    }

    public async Task UnregisterAsync(string id)
    {
        Record("unregister:" + id);
        UnregisterEntered.TrySetResult();
        if (HoldUnregister is { } hold)
            await hold.Task;

        if (UnregisterException is { } ex)
            throw ex;
    }

    public void Activate(string id) => Activated?.Invoke(id);

    private void Record(string call)
    {
        lock (_lock)
            _calls.Add(call);
    }
}
