// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Blazor.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace SmokeTestApp;

public static class Program
{
    internal const string FaultGateName = "smoke-sample-gate";

    [STAThread]
    public static void Main(string[] args)
    {
        var fault = SmokeFault.FromEnvironment();

        var builder = HermesBlazorAppBuilder.CreateDefault(args);
        builder.ConfigureWindow(options =>
        {
            options.Title = "Hermes Smoke Test App";
            options.Width = 640;
            options.Height = 480;
        });
        builder.RootComponents.Add<App>("#app");
        builder.Services.AddSingleton(fault);
        builder.Services.AddHermesSmokeCheck<PassingCheck>();

        if (fault.Kind == SmokeFaultKind.Check)
            builder.Services.AddHermesSmokeCheck<FailingCheck>();

        // Registered but never completed, so the run must time out waiting for it.
        if (fault.Kind == SmokeFaultKind.Gate)
            builder.Services.AddHermesSmokeGate(FaultGateName);

        var app = builder.Build();
        app.Run();
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
