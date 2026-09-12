// Copyright (c) Mythetech. Licensed under the MIT License.
namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// Bridges the Virtualize render on the home page to the scenario runner.
/// </summary>
/// <remarks>
/// Virtualize's first render calls into Blazor._internal.Virtualize in blazor.webview.js. When
/// that script is older than the Components.Web supplied by the shared framework, the call throws
/// and the JS side never calls back; .NET 11 RC1 added setAnchorMode, which the 10.x script has no
/// function for. So the faithful assertion is that the round trip completed, not that the page
/// rendered. Virtualize starts with a visible item capacity of zero and only raises it from the JS
/// spacer callback, which means an ItemsProviderRequest with a non-zero Count cannot happen unless
/// the interop closed.
/// </remarks>
public sealed class VirtualizeProbe
{
    private readonly TaskCompletionSource<bool> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string? Error { get; private set; }

    public void ReportInteropCompleted() => _completion.TrySetResult(true);

    public void ReportError(string error)
    {
        Error = error;
        _completion.TrySetResult(false);
    }

    public async Task<bool> WaitAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await _completion.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Error ??= $"Virtualize never requested a measured range within {timeout.TotalSeconds:F0}s, " +
                      "so blazor.webview.js never called back into the component";
            return false;
        }
    }
}
