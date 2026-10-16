// Copyright (c) Mythetech. Licensed under the MIT License.
#if MACOS
using System.Runtime.Versioning;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Platforms.macOS;
using Xunit;

namespace Hermes.Tests.Platforms;

// These never reach the native library, which the test run does not load.
[SupportedOSPlatform("macos")]
public sealed class MacGlobalHotkeyBackendTests
{
    [Fact]
    public void ToResult_NoErr_IsRegisteredWithTheCanonicalAccelerator()
    {
        var result = MacGlobalHotkeyBackend.ToResult(0, HotkeyAccelerator.Parse("shift+cmd+alt+f12", "accelerator"));

        Assert.Equal(new HermesHotkeyResult(HermesHotkeyStatus.Registered, "Ctrl+Alt+Shift+F12", null), result);
    }

    [Fact]
    public void ToResult_HotKeyExists_IsAConflict()
    {
        var result = MacGlobalHotkeyBackend.ToResult(-9878, HotkeyAccelerator.Parse("Ctrl+Space", "accelerator"));

        Assert.Equal(HermesHotkeyStatus.Conflict, result.Status);
        Assert.Null(result.BoundAccelerator);
        Assert.Equal("Ctrl+Space is already taken by another app or a system shortcut.", result.Reason);
    }

    [Theory]
    [InlineData("Alt+K")]
    [InlineData("Option+Shift+K")]
    public void ToResult_InternalErrorForAnOptionOnlyHotkey_IsUnsupported(string accelerator)
    {
        var result = MacGlobalHotkeyBackend.ToResult(-9868, HotkeyAccelerator.Parse(accelerator, "accelerator"));

        Assert.Equal(HermesHotkeyStatus.Unsupported, result.Status);
        Assert.Contains("Option or Option+Shift", result.Reason);
    }

    [Theory]
    [InlineData(-9868, "Ctrl+Alt+K")]
    [InlineData(-9868, "Meta+Alt+K")]
    [InlineData(-50, "Ctrl+K")]
    public void ToResult_AnyOtherStatus_IsFailedWithTheCode(int status, string accelerator)
    {
        var result = MacGlobalHotkeyBackend.ToResult(status, HotkeyAccelerator.Parse(accelerator, "accelerator"));

        Assert.Equal(HermesHotkeyStatus.Failed, result.Status);
        Assert.Contains($"OSStatus {status}", result.Reason);
    }

    [Theory]
    [InlineData("Ctrl+Insert", "Insert")]
    [InlineData("Ctrl+Alt+F21", "F21")]
    [InlineData("Shift+F24", "F24")]
    public async Task Register_AKeyMacKeyboardsLack_IsUnsupported(string accelerator, string key)
    {
        var backend = new MacGlobalHotkeyBackend();

        var result = await backend.RegisterAsync("id", HotkeyAccelerator.Parse(accelerator, "accelerator"), "id", CancellationToken.None);

        Assert.Equal(new HermesHotkeyResult(HermesHotkeyStatus.Unsupported, null, $"Mac keyboards have no {key} key."), result);
    }

    [Fact]
    public async Task Register_WithACanceledToken_ThrowsBeforeAnyNativeCall()
    {
        var backend = new MacGlobalHotkeyBackend();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => backend.RegisterAsync("id", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "id", cts.Token));
    }

    [Fact]
    public async Task Unregister_OfAnIdItNeverRegistered_DoesNothing()
    {
        var backend = new MacGlobalHotkeyBackend();

        await backend.UnregisterAsync("never-registered");
    }

    [Fact]
    public void APressWithAnUnknownNumber_RaisesNothing()
    {
        var backend = new MacGlobalHotkeyBackend();
        var raised = new List<string>();
        backend.Activated += raised.Add;

        backend.RaisePressed(42);

        Assert.Empty(raised);
    }
}
#endif
