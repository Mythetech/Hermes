// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes;

namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// Proves a thread-pool <c>Invoke</c> made after the message loop has ended finishes once the window is
/// disposed, and that one made after disposal throws <see cref="ObjectDisposedException"/>, instead of hanging.
/// </summary>
public static class InvokeAfterLoopProbe
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);
    private static HermesWindow? _window;
    private static Task<string>? _beforeDispose;

    /// <summary>Call after <c>Run()</c> returns and before the app is disposed.</summary>
    public static void Start(HermesWindow window)
    {
        _window = window;
        _beforeDispose = Task.Run(() => Attempt(window));
    }

    /// <summary>Call after the app has been disposed. Null when both invokes finished as expected.</summary>
    public static string? Failure()
    {
        if (_window is null || _beforeDispose is null)
            return "the invoke probe never started";

        if (!_beforeDispose.Wait(Wait))
            return "an Invoke made after the loop ended was still waiting after the window was disposed";

        if (_beforeDispose.Result is not ("ran" or "disposed"))
            return $"an Invoke made after the loop ended {_beforeDispose.Result}";

        var afterDispose = Task.Run(() => Attempt(_window));
        if (!afterDispose.Wait(Wait))
            return "an Invoke made after the window was disposed hung";

        return afterDispose.Result == "disposed"
            ? null
            : $"an Invoke made after the window was disposed {afterDispose.Result} instead of throwing ObjectDisposedException";
    }

    private static string Attempt(HermesWindow window)
    {
        try
        {
            window.Invoke(() => { });
            return "ran";
        }
        catch (ObjectDisposedException)
        {
            return "disposed";
        }
        catch (Exception ex)
        {
            return $"threw {ex.GetType().Name}";
        }
    }
}
