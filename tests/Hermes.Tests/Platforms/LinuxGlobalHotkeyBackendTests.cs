// Copyright (c) Mythetech. Licensed under the MIT License.
#if LINUX
using System.Runtime.Versioning;
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Hermes.Platforms.Linux;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("linux")]
public sealed class LinuxGlobalHotkeyBackendTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    private readonly List<(int Number, string Id, string Description, string Keysym, uint Modifiers, string Trigger)> _registered = [];
    private readonly List<int> _unregistered = [];

    private LinuxGlobalHotkeyBackend CreateBackend() => new(
        (number, id, description, keysym, modifiers, trigger) => _registered.Add((number, id, description, keysym, modifiers, trigger)),
        _unregistered.Add);

    [Fact]
    public async Task Register_SendsTheKeysymModifiersAndPortalTrigger_AndReportsTheAccelerator()
    {
        var backend = CreateBackend();

        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+Alt+Shift+F12", "accelerator"), "Open the prompt", CancellationToken.None);
        var sent = Assert.Single(_registered);
        backend.OnRegistered(sent.Number, 0, null, null);

        Assert.Equal(("open", "Open the prompt", "F12", 0xDu, "CTRL+ALT+SHIFT+F12"), (sent.Id, sent.Description, sent.Keysym, sent.Modifiers, sent.Trigger));
        Assert.Equal(new HermesHotkeyResult(HermesHotkeyStatus.Registered, "Ctrl+Alt+Shift+F12", null), await registration.WaitAsync(s_timeout));
    }

    [Fact]
    public async Task Register_OnWayland_ReportsTheDesktopsTriggerDescription()
    {
        var backend = CreateBackend();

        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Meta+K", "accelerator"), "open", CancellationToken.None);
        backend.OnRegistered(Assert.Single(_registered).Number, 0, "Press <Super>j", null);

        Assert.Equal("Press <Super>j", (await registration.WaitAsync(s_timeout)).BoundAccelerator);
    }

    [Fact]
    public async Task Presses_AreReportedById_UntilTheIdIsUnregistered()
    {
        var backend = CreateBackend();
        var pressed = new List<string>();
        backend.Activated += pressed.Add;
        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", CancellationToken.None);
        var number = Assert.Single(_registered).Number;
        backend.OnRegistered(number, 0, null, null);
        await registration.WaitAsync(s_timeout);

        backend.OnActivated(number);
        await backend.UnregisterAsync("open").WaitAsync(s_timeout);
        await backend.UnregisterAsync("open").WaitAsync(s_timeout);
        backend.OnActivated(number);

        Assert.Equal(new[] { "open" }, pressed);
        Assert.Equal(new[] { number }, _unregistered);
    }

    [Fact]
    public async Task ARefusedRegistration_DropsItsPresses_AndNeedsNoUnregister()
    {
        var backend = CreateBackend();
        var pressed = new List<string>();
        backend.Activated += pressed.Add;
        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", CancellationToken.None);
        var number = Assert.Single(_registered).Number;

        backend.OnRegistered(number, 1, null, "Another application already uses this shortcut.");
        backend.OnActivated(number);
        await backend.UnregisterAsync("open").WaitAsync(s_timeout);

        Assert.Equal(HermesHotkeyStatus.Conflict, (await registration.WaitAsync(s_timeout)).Status);
        Assert.Empty(pressed);
        Assert.Empty(_unregistered);
    }

    [Fact]
    public async Task AnActivatedHandlerThatThrows_IsRoutedInsteadOfUnwindingIntoGtk()
    {
        var backend = CreateBackend();
        var boom = new InvalidOperationException("hotkey handler boom");
        Exception? routed = null;
        Action<Exception> sink = ex =>
        {
            if (ReferenceEquals(ex, boom))
                routed = ex;
        };
        backend.Activated += _ => throw boom;
        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", CancellationToken.None);
        var number = Assert.Single(_registered).Number;
        backend.OnRegistered(number, 0, null, null);
        await registration.WaitAsync(s_timeout);

        HermesApplication.DispatcherUnhandledException += sink;
        try
        {
            backend.OnActivated(number);
        }
        finally
        {
            HermesApplication.DispatcherUnhandledException -= sink;
        }

        Assert.Same(boom, routed);
    }

    [Fact]
    public async Task ACancel_CompletesTheRegistrationAsFailed_WithoutAnyAnswerFromTheNativeSide()
    {
        var backend = CreateBackend();
        using var cts = new CancellationTokenSource();
        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", cts.Token);
        var number = Assert.Single(_registered).Number;

        await cts.CancelAsync();

        var result = await registration.WaitAsync(s_timeout);
        Assert.Equal(HermesHotkeyStatus.Failed, result.Status);
        Assert.Equal(new[] { number }, _unregistered);
    }

    [Fact]
    public async Task ALateNativeAnswerAfterACancel_ChangesNothing()
    {
        var backend = CreateBackend();
        using var cts = new CancellationTokenSource();
        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", cts.Token);
        var number = Assert.Single(_registered).Number;
        await cts.CancelAsync();
        var canceled = await registration.WaitAsync(s_timeout);

        backend.OnRegistered(number, 0, null, null);

        Assert.Equal(HermesHotkeyStatus.Failed, canceled.Status);
        Assert.Equal(new[] { number }, _unregistered);
    }

    [Fact]
    public async Task APressForACanceledRegistration_RaisesNothing()
    {
        var backend = CreateBackend();
        var pressed = new List<string>();
        backend.Activated += pressed.Add;
        using var cts = new CancellationTokenSource();
        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", cts.Token);
        var number = Assert.Single(_registered).Number;
        await cts.CancelAsync();
        await registration.WaitAsync(s_timeout);

        backend.OnRegistered(number, 0, null, null);
        backend.OnActivated(number);

        Assert.Empty(pressed);
    }

    [Fact]
    public async Task ACancelAfterTheRegistrationCompleted_DoesNotUnregister()
    {
        var backend = CreateBackend();
        using var cts = new CancellationTokenSource();
        var registration = backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", cts.Token);
        backend.OnRegistered(Assert.Single(_registered).Number, 0, null, null);
        var result = await registration.WaitAsync(s_timeout);

        await cts.CancelAsync();

        Assert.Equal(HermesHotkeyStatus.Registered, result.Status);
        Assert.Empty(_unregistered);
    }

    [Fact]
    public async Task ACanceledTokenBeforeTheCall_Throws_AndRegistersNothing()
    {
        var backend = CreateBackend();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => backend.RegisterAsync("open", HotkeyAccelerator.Parse("Ctrl+K", "accelerator"), "open", cts.Token));

        Assert.Empty(_registered);
    }
}
#endif
