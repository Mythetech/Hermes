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

        return new HermesHotkeyResult(HermesHotkeyStatus.Registered, HotkeyAccelerator.ToCanonicalString(accelerator), null);
    }

    public Task UnregisterAsync(string id)
    {
        Record("unregister:" + id);
        return UnregisterException is { } ex ? Task.FromException(ex) : Task.CompletedTask;
    }

    public void Activate(string id) => Activated?.Invoke(id);

    private void Record(string call)
    {
        lock (_lock)
            _calls.Add(call);
    }
}
