// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;

namespace Hermes.Hotkeys;

/// <summary>
/// Maps hotkey ids to the numbers a native backend reports, and completes registrations from its answers.
/// Numbers are never reused, so a press reported after an unregister finds no id.
/// </summary>
internal sealed class NativeHotkeyTable
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, string> _idsByNumber = [];
    private readonly Dictionary<string, int> _numbersById = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Pending> _pending = [];
    private int _lastNumber;

    /// <summary>Starts a registration; <paramref name="completion"/> finishes when <see cref="Complete"/> reports its number.</summary>
    public int Begin(string id, string canonicalAccelerator, out Task<HermesHotkeyResult> completion)
    {
        // Native answers arrive on the platform's thread, which must never run the caller's continuation.
        var source = new TaskCompletionSource<HermesHotkeyResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        completion = source.Task;

        lock (_lock)
        {
            var number = ++_lastNumber;
            _idsByNumber[number] = id;
            _numbersById[id] = number;
            _pending[number] = new Pending(source, canonicalAccelerator);
            return number;
        }
    }

    /// <summary>
    /// Completes a registration with a native status (the <see cref="HermesHotkeyStatus"/> values); a refused one
    /// stops mapping its number, and a registered one without a bound trigger reports the requested accelerator.
    /// Returns false when the number was not pending, so a late answer changes nothing.
    /// </summary>
    public bool Complete(int number, int nativeStatus, string? boundTrigger, string? reason)
    {
        var status = ToStatus(nativeStatus);
        Pending pending;
        lock (_lock)
        {
            if (!_pending.Remove(number, out pending))
                return false;

            if (status != HermesHotkeyStatus.Registered && _idsByNumber.Remove(number, out var id)
                && _numbersById.TryGetValue(id, out var current) && current == number)
            {
                _numbersById.Remove(id);
            }
        }

        pending.Source.TrySetResult(status == HermesHotkeyStatus.Registered
            ? new HermesHotkeyResult(status, boundTrigger ?? pending.CanonicalAccelerator, null)
            : new HermesHotkeyResult(status, null, reason ?? "The platform did not register the hotkey."));
        return true;
    }

    /// <summary>Forgets an id and returns its number, or null when the id is not registered.</summary>
    public int? Remove(string id)
    {
        lock (_lock)
        {
            if (!_numbersById.Remove(id, out var number))
                return null;

            _idsByNumber.Remove(number);
            return number;
        }
    }

    /// <summary>The id a pressed number belongs to, or null once it was unregistered or refused.</summary>
    public string? IdFor(int number)
    {
        lock (_lock)
            return _idsByNumber.GetValueOrDefault(number);
    }

    private static HermesHotkeyStatus ToStatus(int nativeStatus) => nativeStatus switch
    {
        0 => HermesHotkeyStatus.Registered,
        1 => HermesHotkeyStatus.Conflict,
        2 => HermesHotkeyStatus.Denied,
        3 => HermesHotkeyStatus.Unsupported,
        _ => HermesHotkeyStatus.Failed,
    };

    private readonly record struct Pending(TaskCompletionSource<HermesHotkeyResult> Source, string CanonicalAccelerator);
}
