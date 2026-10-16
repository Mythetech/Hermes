// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Menu;

namespace Hermes.Testing;

/// <summary>
/// An in-memory global hotkey backend for Hermes's own tests, installed through <c>GlobalHotkeys.SetBackendForTesting</c>.
/// App tests should substitute <c>IGlobalHotkeys</c> instead. It records registrations and raises presses on demand.
/// </summary>
public sealed class RecordingGlobalHotkeyBackend : IGlobalHotkeyBackend
{
    private readonly List<RecordedHotkey> _registered = [];
    private readonly List<string> _unregistered = [];
    private readonly Dictionary<string, HermesHotkeyResult> _resultsByAccelerator = new(StringComparer.Ordinal);

    public bool IsSupported { get; set; } = true;

    public string? UnsupportedReason { get; set; }

    /// <summary>Return this result when the facade registers this accelerator, given in canonical form such as "Ctrl+Shift+K"; others register.</summary>
    public void SetResult(string accelerator, HermesHotkeyResult result) => _resultsByAccelerator[accelerator] = result;

    public void ClearResults() => _resultsByAccelerator.Clear();

    /// <summary>Every registration the facade passed down, in order.</summary>
    public IReadOnlyList<RecordedHotkey> Registered => _registered;

    /// <summary>Every id the facade unregistered, in order.</summary>
    public IReadOnlyList<string> Unregistered => _unregistered;

    public event Action<string>? Activated;

    public Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description)
    {
        var canonical = HotkeyAccelerator.ToCanonicalString(accelerator);
        _registered.Add(new RecordedHotkey(id, canonical, description));

        return Task.FromResult(_resultsByAccelerator.TryGetValue(canonical, out var result)
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
