// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;
using Xunit;

namespace Hermes.Tests.Hotkeys;

[Collection(GlobalHotkeysCollection.Name)]
public sealed class GlobalHotkeysCancellationTests : IDisposable
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);
    private readonly ControlledGlobalHotkeyBackend _backend = new();

    public GlobalHotkeysCancellationTests()
    {
        GlobalHotkeys.SetBackendForTesting(_backend);
    }

    public void Dispose()
    {
        _backend.HoldRegister?.TrySetResult();
        _backend.HoldUnregister?.TrySetResult();
        GlobalHotkeys.SetBackendForTesting(null);
    }

    [Fact]
    public async Task ATokenCanceledBeforeTheCall_Throws_NeverReachesTheBackend_AndLeavesTheIdFree()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K", null, cts.Token).WaitAsync(s_timeout));
        var again = await GlobalHotkeys.RegisterAsync("open", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(new[] { "register:open" }, _backend.Calls);
        Assert.Equal(HermesHotkeyStatus.Registered, again.Status);
    }

    [Fact]
    public async Task ACancelWhileWaitingForTheGate_ThrowsAndNeverReachesTheBackend()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.HoldRegister = hold;
        var first = GlobalHotkeys.RegisterAsync("first", "Ctrl+Shift+K");
        using var cts = new CancellationTokenSource();
        var second = GlobalHotkeys.RegisterAsync("second", "Ctrl+Shift+L", null, cts.Token);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WaitAsync(s_timeout));
        hold.SetResult();
        var firstResult = await first.WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Registered, firstResult.Status);
        Assert.Equal(new[] { "register:first" }, _backend.Calls);
    }

    [Fact]
    public async Task ACancelWhileTheBackendIsInFlight_WithARegisteredResult_UnregistersAndThrows()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.HoldRegister = hold;
        using var cts = new CancellationTokenSource();
        var register = GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K", null, cts.Token);

        await cts.CancelAsync();
        hold.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => register.WaitAsync(s_timeout));
        _backend.HoldRegister = null;
        var again = await GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(new[] { "register:x", "unregister:x", "register:x" }, _backend.Calls);
        Assert.Equal(HermesHotkeyStatus.Registered, again.Status);
    }

    [Fact]
    public async Task ACancelWhileInFlight_WithTheBackendThrowing_PropagatesTheBackendException_AndFreesTheId()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.HoldRegister = hold;
        _backend.RegisterException = new InvalidOperationException("register boom");
        using var cts = new CancellationTokenSource();
        var register = GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K", null, cts.Token);

        await cts.CancelAsync();
        hold.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => register.WaitAsync(s_timeout));
        _backend.HoldRegister = null;
        _backend.RegisterException = null;
        var again = await GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Registered, again.Status);
    }

    [Fact]
    public async Task ACancelWhileInFlight_WhereTheRollbackThrows_PropagatesItAndKeepsTheRegistration()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.HoldRegister = hold;
        _backend.UnregisterException = new InvalidOperationException("unregister boom");
        using var cts = new CancellationTokenSource();
        var register = GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K", null, cts.Token);

        await cts.CancelAsync();
        hold.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => register.WaitAsync(s_timeout));
        _backend.HoldRegister = null;
        var again = await GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Conflict, again.Status);
    }

    [Fact]
    public async Task UnregisterWithATokenCanceledWhileTheGateIsHeld_Throws_AndKeepsTheRegistration()
    {
        await GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K").WaitAsync(s_timeout);
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.HoldRegister = hold;
        var holder = GlobalHotkeys.RegisterAsync("other", "Ctrl+Shift+L");
        using var cts = new CancellationTokenSource();
        var unregister = GlobalHotkeys.UnregisterAsync("x", cts.Token);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unregister.WaitAsync(s_timeout));
        hold.SetResult();
        await holder.WaitAsync(s_timeout);
        _backend.HoldRegister = null;
        var again = await GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.DoesNotContain("unregister:x", _backend.Calls);
        Assert.Equal(HermesHotkeyStatus.Conflict, again.Status);
    }

    [Fact]
    public async Task ACancelWhileInFlight_WithARefusedResult_ThrowsWithoutUnregistering_AndFreesTheId()
    {
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _backend.HoldRegister = hold;
        _backend.RegisterResult = new HermesHotkeyResult(HermesHotkeyStatus.Conflict, null, "taken");
        using var cts = new CancellationTokenSource();
        var register = GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K", null, cts.Token);

        await cts.CancelAsync();
        hold.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => register.WaitAsync(s_timeout));
        _backend.HoldRegister = null;
        _backend.RegisterResult = null;
        var again = await GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K").WaitAsync(s_timeout);

        Assert.DoesNotContain("unregister:x", _backend.Calls);
        Assert.Equal(HermesHotkeyStatus.Registered, again.Status);
    }

    [Fact]
    public async Task PressesDuringTheRollbackOfACanceledRegistration_AreDropped()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var received = new List<string>();
        Action<string> handler = received.Add;
        GlobalHotkeys.Activated += handler;
        try
        {
            var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _backend.HoldRegister = hold;
            _backend.HoldUnregister = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var register = GlobalHotkeys.RegisterAsync("x", "Ctrl+Shift+K", null, cts.Token);

            await cts.CancelAsync();
            hold.SetResult();
            await _backend.UnregisterEntered.Task.WaitAsync(s_timeout);
            _backend.Activate("x");
            _backend.HoldUnregister.SetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => register.WaitAsync(s_timeout));
        }
        finally
        {
            GlobalHotkeys.Activated -= handler;
        }

        Assert.Empty(received);
    }
}
