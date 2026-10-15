// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Menu;

namespace Hermes.Testing;

/// <summary>
/// An in-memory global hotkey backend that records registrations and raises presses on demand.
/// </summary>
public sealed class RecordingGlobalHotkeyBackend : IGlobalHotkeyBackend
{
    private readonly List<RecordedHotkey> _registered = [];
    private readonly List<string> _unregistered = [];

    public bool IsSupported { get; set; } = true;

    public string? UnsupportedReason { get; set; }

    /// <summary>Results to return for particular accelerators, keyed by canonical form such as "Ctrl+Shift+K"; others register.</summary>
    public Dictionary<string, HermesHotkeyResult> ResultsByAccelerator { get; } = new(StringComparer.Ordinal);

    /// <summary>Every registration the facade passed down, in order.</summary>
    public IReadOnlyList<RecordedHotkey> Registered => _registered;

    /// <summary>Every id the facade unregistered, in order.</summary>
    public IReadOnlyList<string> Unregistered => _unregistered;

    public event Action<string>? Activated;

    public Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description)
    {
        var canonical = HotkeyAccelerator.ToCanonicalString(accelerator);
        _registered.Add(new RecordedHotkey(id, canonical, description));

        return Task.FromResult(ResultsByAccelerator.TryGetValue(canonical, out var result)
            ? result
            : new HermesHotkeyResult(HermesHotkeyStatus.Registered, canonical, null));
    }

    public Task UnregisterAsync(string id)
    {
        _unregistered.Add(id);
        return Task.CompletedTask;
    }

    /// <summary>Simulate the platform reporting a press of the hotkey with this id.</summary>
    public void Activate(string id) => Activated?.Invoke(id);
}

/// <summary>A registration the recording backend received.</summary>
public sealed record RecordedHotkey(string Id, string Accelerator, string Description);
