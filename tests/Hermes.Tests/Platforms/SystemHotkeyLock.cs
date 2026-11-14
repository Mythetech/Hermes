// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Runtime.Versioning;

namespace Hermes.Tests.Platforms;

/// <summary>
/// Held by tests that register real hotkeys: hotkeys are system-wide, and dotnet test runs one test process per target framework at once.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class SystemHotkeyLock : IDisposable
{
    private readonly Semaphore _semaphore = new(1, 1, @"Local\Hermes.Tests.SystemHotkeys");

    public SystemHotkeyLock()
    {
        if (_semaphore.WaitOne(TimeSpan.FromMinutes(1)))
            return;

        _semaphore.Dispose();
        throw new TimeoutException("Another test process held the system hotkey lock for a minute.");
    }

    public void Dispose()
    {
        _semaphore.Release();
        _semaphore.Dispose();
    }
}
#endif
