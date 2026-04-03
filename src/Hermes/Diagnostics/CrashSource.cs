// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Diagnostics;

/// <summary>
/// Identifies the origin of an intercepted crash.
/// </summary>
public enum CrashSource
{
    /// <summary>Unhandled exception on any thread (AppDomain.UnhandledException).</summary>
    UnhandledException,

    /// <summary>Unobserved task exception (TaskScheduler.UnobservedTaskException).</summary>
    UnobservedTask,

    /// <summary>Native signal (SIGSEGV, SIGABRT, etc.) or Windows SEH.</summary>
    NativeSignal,

    /// <summary>WebView render/browser process crash.</summary>
    WebViewCrash
}
