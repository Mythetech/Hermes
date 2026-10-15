// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Collections.Concurrent;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.DataTransfer;

/// <summary>
/// Turns one window's drop reports into registered drops, raises Dropped, and answers the drop script.
/// </summary>
internal sealed class DropCoordinator
{
    private readonly object _owner;
    private readonly IHermesWindowBackend _backend;
    private readonly DropRegistry _registry;
    private readonly Action<HermesDrop> _raiseDropped;
    private readonly ConcurrentQueue<(HermesDataPackage Data, TaskCompletionSource<HermesDrop> Completion)> _testCaptures = new();

    public DropCoordinator(object owner, IHermesWindowBackend backend, DropRegistry registry, Action<HermesDrop> raiseDropped)
    {
        _owner = owner;
        _backend = backend;
        _registry = registry;
        _raiseDropped = raiseDropped;
    }

    public void Attach()
    {
        _backend.DropReported += OnDropReported;
        _backend.Closing += OnClosing;
    }

    /// <summary>Stands in for the platform's capture of the next reported drop.</summary>
    public Task<HermesDrop> QueueTestCapture(HermesDataPackage data)
    {
        var completion = new TaskCompletionSource<HermesDrop>(TaskCreationOptions.RunContinuationsAsynchronously);
        _testCaptures.Enqueue((data, completion));
        return completion.Task;
    }

    private void OnDropReported(HermesDropReport report)
    {
        if (report.Kind == HermesDropReportKind.Reset)
        {
            _registry.Release(_owner);
            return;
        }

        var data = report.Data;
        TaskCompletionSource<HermesDrop>? completion = null;
        if (_testCaptures.TryDequeue(out var capture))
        {
            data = capture.Data;
            completion = capture.Completion;
        }

        if (data is null || data.IsEmpty)
        {
            Reply(report.Sequence, dropId: null);
            return;
        }

        var drop = _registry.Register(_owner, report.X, report.Y, data);
        _raiseDropped(drop);
        Reply(report.Sequence, drop.Id);
        completion?.TrySetResult(drop);
    }

    private void OnClosing() => _registry.Release(_owner);

    private void Reply(long sequence, string? dropId) =>
        _backend.SendWebMessage(dropId is null
            ? $$"""{"type":"hermes-drop-ready","seq":{{sequence}}}"""
            : $$"""{"type":"hermes-drop-ready","seq":{{sequence}},"dropId":"{{dropId}}"}""");
}
