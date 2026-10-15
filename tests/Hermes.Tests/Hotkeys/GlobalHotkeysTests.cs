// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Hotkeys;

[Collection(GlobalHotkeysCollection.Name)]
public sealed class GlobalHotkeysTests : IDisposable
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);
    private readonly RecordingGlobalHotkeyBackend _backend = new();

    public GlobalHotkeysTests()
    {
        GlobalHotkeys.SetBackendForTesting(_backend);
    }

    public void Dispose() => GlobalHotkeys.SetBackendForTesting(null);

    [Fact]
    public async Task Register_PassesTheCanonicalAcceleratorAndDescription()
    {
        var result = await GlobalHotkeys.RegisterAsync("open", "cmd+shift+k", "Open the prompt").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Registered, result.Status);
        Assert.Equal(new RecordedHotkey("open", "Ctrl+Shift+K", "Open the prompt"), Assert.Single(_backend.Registered));
    }

    [Fact]
    public async Task Register_DefaultsTheDescriptionToTheId()
    {
        await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal("open", Assert.Single(_backend.Registered).Description);
    }

    [Fact]
    public async Task DuplicateId_IsAConflict()
    {
        await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);

        var second = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+J").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Conflict, second.Status);
        Assert.Single(_backend.Registered);
    }

    [Fact]
    public async Task DuplicateAccelerator_InAnotherSpelling_IsAConflict()
    {
        await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);

        var second = await GlobalHotkeys.RegisterAsync("other", "Shift+Cmd+k").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Conflict, second.Status);
        Assert.Single(_backend.Registered);
    }

    [Fact]
    public async Task TheLimit_IsEnforced()
    {
        const string keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string[] modifiers = ["Ctrl", "Ctrl+Alt", "Ctrl+Shift", "Ctrl+Alt+Shift"];
        for (var i = 0; i < GlobalHotkeys.MaxRegistrations; i++)
        {
            var accelerator = $"{modifiers[i / keys.Length]}+{keys[i % keys.Length]}";
            var result = await GlobalHotkeys.RegisterAsync("hotkey" + i, accelerator).WaitAsync(s_timeout);
            Assert.Equal(HermesHotkeyStatus.Registered, result.Status);
        }

        var over = await GlobalHotkeys.RegisterAsync("one-too-many", "Alt+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.LimitExceeded, over.Status);
    }

    [Fact]
    public async Task ARefusedRegistration_LeavesNoTrace()
    {
        _backend.ResultsByAccelerator["Ctrl+Shift+K"] = new HermesHotkeyResult(HermesHotkeyStatus.Conflict, null, "Another app owns it.");

        var refused = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
        _backend.ResultsByAccelerator.Clear();
        var retried = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Conflict, refused.Status);
        Assert.Equal(HermesHotkeyStatus.Registered, retried.Status);
    }

    [Fact]
    public async Task AnUnsupportedBackend_ReturnsUnsupportedWithItsReason()
    {
        _backend.IsSupported = false;
        _backend.UnsupportedReason = "No global shortcuts here.";

        var result = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Unsupported, result.Status);
        Assert.Equal("No global shortcuts here.", result.Reason);
        Assert.Empty(_backend.Registered);
    }

    [Fact]
    public async Task Activated_CarriesTheId()
    {
        // xUnit installs its own context after construction, which would post handlers asynchronously.
        SynchronizationContext.SetSynchronizationContext(null);
        var received = new List<string>();
        Action<string> handler = received.Add;
        GlobalHotkeys.Activated += handler;
        try
        {
            await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
            _backend.Activate("open");
        }
        finally
        {
            GlobalHotkeys.Activated -= handler;
        }

        Assert.Equal(new[] { "open" }, received);
    }

    [Fact]
    public async Task Unregister_StopsActivations_EvenForALatePlatformPress()
    {
        // xUnit installs its own context after construction, which would post handlers asynchronously.
        SynchronizationContext.SetSynchronizationContext(null);
        var received = new List<string>();
        Action<string> handler = received.Add;
        GlobalHotkeys.Activated += handler;
        try
        {
            await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
            await GlobalHotkeys.UnregisterAsync("open").WaitAsync(s_timeout);
            _backend.Activate("open");
        }
        finally
        {
            GlobalHotkeys.Activated -= handler;
        }

        Assert.Empty(received);
        Assert.Equal(new[] { "open" }, _backend.Unregistered);
    }

    [Fact]
    public async Task UnregisteringAnUnknownId_DoesNothing()
    {
        await GlobalHotkeys.UnregisterAsync("never-registered").WaitAsync(s_timeout);

        Assert.Empty(_backend.Unregistered);
    }

    [Fact]
    public async Task HandlerThatThrows_DoesNotStopOthers_AndIsRouted()
    {
        // xUnit installs its own context after construction, which would post handlers asynchronously.
        SynchronizationContext.SetSynchronizationContext(null);
        var boom = new InvalidOperationException("hotkey handler boom");
        var secondRan = false;
        Exception? routed = null;
        Action<Exception> sink = ex =>
        {
            if (ReferenceEquals(ex, boom))
                routed = ex;
        };
        Action<string> first = _ => throw boom;
        Action<string> second = _ => secondRan = true;
        GlobalHotkeys.Activated += first;
        GlobalHotkeys.Activated += second;
        HermesApplication.DispatcherUnhandledException += sink;
        try
        {
            await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
            _backend.Activate("open");
        }
        finally
        {
            GlobalHotkeys.Activated -= first;
            GlobalHotkeys.Activated -= second;
            HermesApplication.DispatcherUnhandledException -= sink;
        }

        Assert.True(secondRan);
        Assert.Same(boom, routed);
    }

    [Fact]
    public async Task Activated_RunsOnTheSubscribersContext()
    {
        var context = new RecordingContext();
        var received = new List<string>();
        Action<string> handler = received.Add;
        SynchronizationContext.SetSynchronizationContext(context);
        GlobalHotkeys.Activated += handler;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);
            _backend.Activate("open");
        }
        finally
        {
            GlobalHotkeys.Activated -= handler;
        }

        Assert.Equal(1, context.Posts);
        Assert.Equal(new[] { "open" }, received);
    }

    [Theory]
    [InlineData("", "Ctrl+K")]
    [InlineData("open", "K")]
    [InlineData("open", "Alt+F4")]
    [InlineData("open", "Ctrl+/")]
    public async Task InvalidArguments_Throw(string id, string accelerator) =>
        await Assert.ThrowsAsync<ArgumentException>(() => GlobalHotkeys.RegisterAsync(id, accelerator));

    private sealed class RecordingContext : SynchronizationContext
    {
        public int Posts { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            Posts++;
            d(state);
        }
    }
}
