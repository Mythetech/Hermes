// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Blazor.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Hermes.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeModeBuilderTests
{
    private readonly SmokeHarness _harness = new();
    private SmokeSession? _session;

    private HermesBlazorApp Build(bool smoke = true, Action<HermesBlazorAppBuilder>? configure = null)
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();
        builder.ForceDevServer(false);
        builder.RootComponents.Add<Probe>("#app");
        if (smoke)
        {
            _session = _harness.CreateSession(exitWhenDone: false);
            builder.UseSmokeSessionForTest(_session);
        }
        configure?.Invoke(builder);

        // Build installs the Hermes synchronization context on this thread; restore it so later awaits
        // do not post into the recording backend's queue, which nothing pumps.
        var previous = SynchronizationContext.Current;
        try
        {
            return HermesBlazorAppBuilder.BuildForTest(builder, new RecordingWindowBackend());
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public async Task WrapsEachRootComponent()
    {
        // Two roots so a deleted SetExpectedRootCount call (defaulting to 1) cannot pass this test
        // by coincidence the way a single-root count of 1 would.
        var app = Build(configure: b => b.RootComponents.Add<SecondProbe>("#second"));
        try
        {
            Assert.Equal(2, app.RootComponents.PendingComponentTypes.Count);
            Assert.All(app.RootComponents.PendingComponentTypes, t => Assert.Equal(typeof(SmokeRoot), t));
            Assert.Equal(2, app.Services.GetRequiredService<HermesSmokeRuntime>().ExpectedRootCount);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task WithoutSmokeMode_LeavesRootsAlone_AndTheSessionIsANoOp()
    {
        var app = Build(smoke: false);
        try
        {
            Assert.Equal(new[] { typeof(Probe) }, app.RootComponents.PendingComponentTypes);
            Assert.False(app.Services.GetRequiredService<IHermesSmokeSession>().IsEnabled);
            Assert.Null(app.Services.GetService<HermesSmokeRuntime>());
            Assert.DoesNotContain(app.Services.GetServices<ILoggerProvider>(), p => p is SmokeLoggerProvider);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task MarksWindowShown_AndRegistersTheActiveSession()
    {
        var app = Build();
        try
        {
            Assert.Contains(_harness.Lines, line => line.StartsWith("HERMES_SMOKE_MILESTONE: window-shown", StringComparison.Ordinal));
            Assert.True(app.Services.GetRequiredService<IHermesSmokeSession>().IsEnabled);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task RegisteredGates_HoldTheVerdict_UntilCompleted()
    {
        var app = Build(configure: b => b.Services.AddHermesSmokeGate("app-ready"));
        try
        {
            var run = app.Services.GetRequiredService<HermesSmokeRuntime>().OnRootRenderedAsync(app.Services);
            Assert.False(_session!.IsFinished);

            app.Services.GetRequiredService<IHermesSmokeSession>().CompleteGate("app-ready");
            await run;

            Assert.Equal("HERMES_SMOKE_RESULT: PASSED (0 checks)", _harness.Lines[^1]);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task ErrorLogs_FailTheRun()
    {
        var app = Build();
        try
        {
            app.Services.GetRequiredService<ILogger<SmokeModeBuilderTests>>().LogError("broken");

            Assert.Contains(_harness.Lines, line => line.StartsWith("HERMES_SMOKE_ERROR: log: Error:", StringComparison.Ordinal));
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task NativeDialogs_AreGuarded()
    {
        var app = Build();
        try
        {
            Assert.IsType<SmokeDialogBackend>(app.MainWindow.Dialogs);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    private sealed class Probe : ComponentBase
    {
    }

    private sealed class SecondProbe : ComponentBase
    {
    }
}
