// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Blazor;

/// <summary>
/// Scheme handler that can be registered with the native backend before the
/// real request handler exists. Early requests block until the inner handler
/// is installed, then delegate. Returns (null, null) if the inner handler is
/// not installed within the timeout, which surfaces as a 404 in the WebView.
/// </summary>
internal sealed class DeferredSchemeHandler
{
    private readonly ManualResetEventSlim _innerReady = new(false);
    private readonly TimeSpan _timeout;
    private Func<string, (Stream? Content, string? ContentType)>? _inner;

    public DeferredSchemeHandler(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    public void SetInner(Func<string, (Stream? Content, string? ContentType)> inner)
    {
        _inner = inner;
        _innerReady.Set();
    }

    public (Stream? Content, string? ContentType) Handle(string url)
    {
        if (!_innerReady.Wait(_timeout))
            return (null, null);

        return _inner!(url);
    }
}
