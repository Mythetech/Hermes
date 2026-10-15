// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;
using Xunit;

namespace Hermes.Tests.Hotkeys;

[Collection(GlobalHotkeysCollection.Name)]
public sealed class GlobalHotkeysBackendFailureTests : IDisposable
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);
    private readonly ControlledGlobalHotkeyBackend _backend = new();

    public GlobalHotkeysBackendFailureTests()
    {
        GlobalHotkeys.SetBackendForTesting(_backend);
    }

    public void Dispose() => GlobalHotkeys.SetBackendForTesting(null);

    [Fact]
    public async Task ABackendRegisterThatThrows_PropagatesAndLeavesNoTrace()
    {
        _backend.RegisterException = new InvalidOperationException("register boom");

        await Assert.ThrowsAsync<InvalidOperationException>(() => GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout));
        _backend.RegisterException = null;
        var retried = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Registered, retried.Status);
    }

    [Fact]
    public async Task Unregister_WaitsForARegisterInFlight()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.HoldRegister = hold;
        var register = GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K");
        var unregister = GlobalHotkeys.UnregisterAsync("open");

        await Task.Delay(100);
        Assert.Equal(new[] { "register:open" }, _backend.Calls);

        hold.SetResult();
        await register.WaitAsync(s_timeout);
        await unregister.WaitAsync(s_timeout);

        Assert.Equal(new[] { "register:open", "unregister:open" }, _backend.Calls);
        _backend.HoldRegister = null;
        var again = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
        Assert.Equal(HermesHotkeyStatus.Registered, again.Status);
    }

    [Fact]
    public async Task APostThatThrows_IsRouted_AndOtherSubscribersStillReceiveThePress()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var boom = new InvalidOperationException("post boom");
        var received = new List<string>();
        Exception? routed = null;
        Action<Exception> sink = ex =>
        {
            if (ReferenceEquals(ex, boom))
                routed = ex;
        };
        Action<string> throwingSide = _ => { };
        Action<string> plain = received.Add;

        SynchronizationContext.SetSynchronizationContext(new ThrowingPostContext(boom));
        GlobalHotkeys.Activated += throwingSide;
        SynchronizationContext.SetSynchronizationContext(null);
        GlobalHotkeys.Activated += plain;
        HermesApplication.DispatcherUnhandledException += sink;
        try
        {
            await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
            _backend.Activate("open");
        }
        finally
        {
            GlobalHotkeys.Activated -= throwingSide;
            GlobalHotkeys.Activated -= plain;
            HermesApplication.DispatcherUnhandledException -= sink;
        }

        Assert.Same(boom, routed);
        Assert.Equal(new[] { "open" }, received);
    }

    [Fact]
    public async Task ABackendUnregisterThatThrows_KeepsTheHotkeyRegistered()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var received = new List<string>();
        Action<string> handler = received.Add;
        GlobalHotkeys.Activated += handler;
        try
        {
            await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
            _backend.UnregisterException = new InvalidOperationException("unregister boom");

            await Assert.ThrowsAsync<InvalidOperationException>(() => GlobalHotkeys.UnregisterAsync("open").WaitAsync(s_timeout));
            var again = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
            _backend.Activate("open");

            Assert.Equal(HermesHotkeyStatus.Conflict, again.Status);
        }
        finally
        {
            GlobalHotkeys.Activated -= handler;
        }

        Assert.Equal(new[] { "open" }, received);
    }

    private sealed class ThrowingPostContext(Exception toThrow) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => throw toThrow;
    }
}
