// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Runtime.Versioning;
using Hermes.Contracts.Hotkeys;
using Xunit;

namespace Hermes.Tests.Platforms;

// Shares GlobalHotkeysCollection with the facade tests, because it swaps the facade's backend too.
[Collection("Global hotkeys")]
[SupportedOSPlatform("windows")]
public sealed class WindowsGlobalHotkeysFacadeTests : IDisposable
{
    public WindowsGlobalHotkeysFacadeTests() => GlobalHotkeys.SetBackendForTesting(null);

    public void Dispose() => GlobalHotkeys.SetBackendForTesting(null);

    [Fact]
    public async Task TheFacade_RegistersThroughWindows()
    {
        var timeout = TimeSpan.FromSeconds(5);

        var result = await GlobalHotkeys.RegisterAsync("windows.facade", "Ctrl+Alt+Shift+F24", "Facade").WaitAsync(timeout);
        await GlobalHotkeys.UnregisterAsync("windows.facade").WaitAsync(timeout);

        Assert.Equal(new HermesHotkeyResult(HermesHotkeyStatus.Registered, "Ctrl+Alt+Shift+F24", null), result);
    }
}
#endif
