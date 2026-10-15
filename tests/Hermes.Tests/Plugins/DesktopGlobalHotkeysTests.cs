// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Contracts.Hotkeys;
using Hermes.Contracts.Plugins;
using Hermes.Plugins;
using Hermes.Testing;
using Hermes.Tests.Hotkeys;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Plugins;

[Collection(GlobalHotkeysCollection.Name)]
public sealed class DesktopGlobalHotkeysTests : IDisposable
{
    private readonly RecordingGlobalHotkeyBackend _backend = new();

    public DesktopGlobalHotkeysTests()
    {
        GlobalHotkeys.SetBackendForTesting(_backend);
    }

    public void Dispose() => GlobalHotkeys.SetBackendForTesting(null);

    [Fact]
    public async Task ForwardsToTheFacade()
    {
        // xUnit installs its own context after construction, which would post handlers asynchronously.
        SynchronizationContext.SetSynchronizationContext(null);
        IGlobalHotkeys hotkeys = new DesktopGlobalHotkeys();
        var received = new List<string>();
        Action<string> handler = received.Add;
        hotkeys.Activated += handler;
        try
        {
            var result = await hotkeys.RegisterAsync("open", "Ctrl+Shift+K", "Open").WaitAsync(TimeSpan.FromSeconds(5));
            _backend.Activate("open");
            await hotkeys.UnregisterAsync("open").WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(HermesHotkeyStatus.Registered, result.Status);
        }
        finally
        {
            hotkeys.Activated -= handler;
        }

        Assert.Equal(new[] { "open" }, received);
        Assert.Equal(new[] { "open" }, _backend.Unregistered);
    }

    [Fact]
    public void BlazorApps_ResolveIGlobalHotkeys()
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, new RecordingWindowBackend());

        Assert.IsType<DesktopGlobalHotkeys>(composition.ServiceProvider.GetRequiredService<IGlobalHotkeys>());
    }
}
