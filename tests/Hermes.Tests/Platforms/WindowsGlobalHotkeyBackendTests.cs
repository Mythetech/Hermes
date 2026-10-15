// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Runtime.Versioning;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Menu;
using Hermes.Platforms.Windows;
using Xunit;

namespace Hermes.Tests.Platforms;

// These register real system-wide hotkeys on F13 and up, which few keyboards have, so nothing else owns them.
[SupportedOSPlatform("windows")]
public sealed class WindowsGlobalHotkeyBackendTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void NativeModifiers_AlwaysAddNoRepeat()
    {
        var all = new Accelerator { Control = true, Alt = true, Shift = true, Meta = true, Key = "K" };

        Assert.Equal(0x400Fu, (uint)WindowsGlobalHotkeyBackend.NativeModifiers(all));
        Assert.Equal(0x4002u, (uint)WindowsGlobalHotkeyBackend.NativeModifiers(new Accelerator { Control = true, Key = "K" }));
    }

    [Fact]
    public void Constructing_StartsNoThread() =>
        Assert.Null(new WindowsGlobalHotkeyBackend().ThreadExit);

    [Fact]
    public async Task AKeyWithoutAWindowsCode_IsUnsupported_AndStartsNoThread()
    {
        var backend = new WindowsGlobalHotkeyBackend();

        var result = await backend.RegisterAsync("numlock", new Accelerator { Control = true, Key = "NumLock" }, "NumLock")
            .WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Unsupported, result.Status);
        Assert.Null(backend.ThreadExit);
    }

    [Fact]
    public async Task Unregister_ReleasesTheCombination()
    {
        var backend = new WindowsGlobalHotkeyBackend();
        var accelerator = Parse("Ctrl+Alt+Shift+F19");

        var first = await backend.RegisterAsync("first", accelerator, "First").WaitAsync(s_timeout);
        await backend.UnregisterAsync("first").WaitAsync(s_timeout);
        var second = await backend.RegisterAsync("second", accelerator, "Second").WaitAsync(s_timeout);
        await backend.UnregisterAsync("second").WaitAsync(s_timeout);

        Assert.Equal(new HermesHotkeyResult(HermesHotkeyStatus.Registered, "Ctrl+Alt+Shift+F19", null), first);
        Assert.Equal(HermesHotkeyStatus.Registered, second.Status);
    }

    [Fact]
    public async Task TheSameCombinationInAnotherBackend_IsAConflict_UntilTheOwnerReleasesIt()
    {
        var owner = new WindowsGlobalHotkeyBackend();
        var other = new WindowsGlobalHotkeyBackend();
        var accelerator = Parse("Ctrl+Alt+Shift+F20");

        var owned = await owner.RegisterAsync("owner", accelerator, "Owner").WaitAsync(s_timeout);
        var refused = await other.RegisterAsync("other", accelerator, "Other").WaitAsync(s_timeout);
        var refusedLeftAThread = other.ThreadExit is not null;
        await owner.UnregisterAsync("owner").WaitAsync(s_timeout);
        var retried = await other.RegisterAsync("other", accelerator, "Other").WaitAsync(s_timeout);
        await other.UnregisterAsync("other").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Registered, owned.Status);
        Assert.Equal(HermesHotkeyStatus.Conflict, refused.Status);
        Assert.NotNull(refused.Reason);
        Assert.False(refusedLeftAThread);
        Assert.Equal(HermesHotkeyStatus.Registered, retried.Status);
    }

    [Fact]
    public async Task RegisteringAHeldIdAgain_ReleasesTheOlderHotkey()
    {
        var backend = new WindowsGlobalHotkeyBackend();
        var other = new WindowsGlobalHotkeyBackend();

        await backend.RegisterAsync("a", Parse("Ctrl+Alt+Shift+F19"), "A").WaitAsync(s_timeout);
        var replaced = await backend.RegisterAsync("a", Parse("Ctrl+Alt+Shift+F20"), "A").WaitAsync(s_timeout);
        var freed = await other.RegisterAsync("b", Parse("Ctrl+Alt+Shift+F19"), "B").WaitAsync(s_timeout);
        await other.UnregisterAsync("b").WaitAsync(s_timeout);
        await backend.UnregisterAsync("a").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Registered, replaced.Status);
        Assert.Equal(HermesHotkeyStatus.Registered, freed.Status);
        Assert.Null(backend.ThreadExit);
    }

    [Fact]
    public async Task UnregisteringTheLastHotkey_EndsTheThread_AndTheNextRegistrationStartsAnother()
    {
        var backend = new WindowsGlobalHotkeyBackend();
        await backend.RegisterAsync("a", Parse("Ctrl+Alt+Shift+F21"), "A").WaitAsync(s_timeout);
        await backend.RegisterAsync("b", Parse("Ctrl+Alt+Shift+F22"), "B").WaitAsync(s_timeout);
        var exit = backend.ThreadExit;
        Assert.NotNull(exit);

        await backend.UnregisterAsync("a").WaitAsync(s_timeout);
        Assert.False(exit.IsCompleted);
        Assert.Same(exit, backend.ThreadExit);

        await backend.UnregisterAsync("b").WaitAsync(s_timeout);
        await exit.WaitAsync(s_timeout);
        Assert.Null(backend.ThreadExit);

        var again = await backend.RegisterAsync("a", Parse("Ctrl+Alt+Shift+F21"), "A").WaitAsync(s_timeout);
        var restarted = backend.ThreadExit;
        await backend.UnregisterAsync("a").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Registered, again.Status);
        Assert.NotNull(restarted);
        Assert.NotSame(exit, restarted);
    }

    [Fact]
    public async Task APress_RaisesActivatedWithItsId_OnTheBackgroundHotkeyThread()
    {
        var backend = new WindowsGlobalHotkeyBackend();
        var pressed = new TaskCompletionSource<(string Id, string? ThreadName, bool IsBackground)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        backend.Activated += id => pressed.TrySetResult((id, Thread.CurrentThread.Name, Thread.CurrentThread.IsBackground));
        await backend.RegisterAsync("first", Parse("Ctrl+Alt+Shift+F21"), "First").WaitAsync(s_timeout);
        await backend.RegisterAsync("second", Parse("Ctrl+Alt+Shift+F22"), "Second").WaitAsync(s_timeout);

        var posted = backend.SimulatePressForTesting("second");
        var (id, threadName, isBackground) = await pressed.Task.WaitAsync(s_timeout);
        await backend.UnregisterAsync("first").WaitAsync(s_timeout);
        await backend.UnregisterAsync("second").WaitAsync(s_timeout);

        Assert.True(posted);
        Assert.Equal("second", id);
        Assert.Equal(WindowsGlobalHotkeyBackend.ThreadName, threadName);
        Assert.True(isBackground);
    }

    [Fact]
    public async Task Completions_DoNotRunOnTheHotkeyThread()
    {
        // xUnit's own context would move the continuation off the hotkey thread whatever the backend did.
        SynchronizationContext.SetSynchronizationContext(null);
        var backend = new WindowsGlobalHotkeyBackend();

        await backend.RegisterAsync("a", Parse("Ctrl+Alt+Shift+F23"), "A").WaitAsync(s_timeout);
        var afterRegister = Thread.CurrentThread.Name;
        await backend.UnregisterAsync("a").WaitAsync(s_timeout);
        var afterUnregister = Thread.CurrentThread.Name;

        Assert.NotEqual(WindowsGlobalHotkeyBackend.ThreadName, afterRegister);
        Assert.NotEqual(WindowsGlobalHotkeyBackend.ThreadName, afterUnregister);
    }

    [Fact]
    public async Task ConcurrentRegistrationsAndUnregistrations_AllComplete_AndLeaveNoThread()
    {
        var backend = new WindowsGlobalHotkeyBackend();
        string[] keys = ["F13", "F14", "F15", "F16", "F17", "F18"];

        var cycles = keys.Select(key => Task.Run(async () =>
        {
            var accelerator = Parse("Ctrl+Alt+Shift+" + key);
            for (var round = 0; round < 20; round++)
            {
                var result = await backend.RegisterAsync($"{key}-{round}", accelerator, key).WaitAsync(s_timeout);
                Assert.Equal(HermesHotkeyStatus.Registered, result.Status);
                await backend.UnregisterAsync($"{key}-{round}").WaitAsync(s_timeout);
            }
        }));
        await Task.WhenAll(cycles).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Null(backend.ThreadExit);
    }

    private static Accelerator Parse(string accelerator) => HotkeyAccelerator.Parse(accelerator, nameof(accelerator));
}
#endif
