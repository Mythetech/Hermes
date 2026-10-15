// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using Hermes.Contracts.DataTransfer;
using Hermes.Platforms.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("windows")]
public sealed class WindowsDragSourceTests
{
    [Fact]
    public async Task StartDragAsync_ReturnsBeforeTheDragRuns_AndDisposeCancelsIt()
    {
        Task<HermesDragResult>? drag = null;
        var completedBeforeDispose = true;

        RunOnSta(() =>
        {
            var backend = new WindowsWindowBackend();
            backend.Initialize(new HermesWindowOptions());
            drag = backend.StartDragAsync(new HermesDataPackage { Text = "drag" });
            completedBeforeDispose = drag.IsCompleted;
            backend.Dispose();
        });

        Assert.False(completedBeforeDispose);
        Assert.Equal(HermesDragResult.Canceled, await drag!);
    }

    [Fact]
    public async Task StartDragAsync_WithNoButtonHeld_IsCanceledWhenTheQueuedDragRuns()
    {
        Task<HermesDragResult>? drag = null;
        var completedByTheLoop = false;

        RunOnSta(() =>
        {
            var backend = new WindowsWindowBackend();
            backend.Initialize(new HermesWindowOptions());
            drag = backend.StartDragAsync(new HermesDataPackage { Text = "drag" });

            var deadline = Environment.TickCount64 + 5000;
            while (!drag.IsCompleted && Environment.TickCount64 < deadline)
            {
                if (PInvoke.PeekMessage(out var msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
                {
                    PInvoke.TranslateMessage(in msg);
                    PInvoke.DispatchMessage(in msg);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }

            completedByTheLoop = drag.IsCompleted;
            backend.Dispose();
        });

        Assert.True(completedByTheLoop);
        Assert.Equal(HermesDragResult.Canceled, await drag!);
    }

    // WindowsWindowBackend.Initialize requires an STA thread, as an app's UI thread is.
    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
#endif
