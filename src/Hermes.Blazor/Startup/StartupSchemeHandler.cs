// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.Diagnostics;

namespace Hermes.Blazor.Startup;

/// <summary>
/// The scheme handler registered with the backend before the window exists. Until
/// the WebViewManager installs itself as the inner handler, requests are answered
/// by the early static handler; afterwards they are forwarded. Nothing here ever
/// blocks, which is what allows the UI thread to pump native events while
/// composition runs.
/// </summary>
internal sealed class StartupSchemeHandler
{
    private readonly EarlyStaticContentHandler _early;
    private volatile Func<string, (Stream? Content, string? ContentType)>? _inner;
    private int _firstRequestLogged;

    public StartupSchemeHandler(EarlyStaticContentHandler early)
    {
        _early = early;
    }

    public bool HasInner => _inner is not null;

    public void SetInner(Func<string, (Stream? Content, string? ContentType)> inner)
    {
        _inner = inner;
    }

    public (Stream? Content, string? ContentType) Handle(string url)
    {
        StartupLog.PhaseOnce("first-request", ref _firstRequestLogged);
        var inner = _inner;
        return inner is not null ? inner(url) : _early.Handle(url);
    }
}
