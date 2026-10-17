// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;

namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// Bridges the drop zone's @onhermesdrop handler to the scenario runner.
/// </summary>
public sealed class DropProbe
{
    private readonly TaskCompletionSource<HermesDropEventArgs> _drop =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Report(HermesDropEventArgs args) => _drop.TrySetResult(args);

    public async Task<HermesDropEventArgs?> WaitAsync(TimeSpan timeout)
    {
        try
        {
            return await _drop.Task.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }
}
