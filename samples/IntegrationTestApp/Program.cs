using Hermes;
using Hermes.Blazor;
using Hermes.Blazor.Diagnostics;
using IntegrationTestApp;
using IntegrationTestApp.TestScenarios;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Check if we're in integration test mode
        var isIntegrationTest = Environment.GetEnvironmentVariable("HERMES_INTEGRATION_TEST") == "1";
        var autoExit = Environment.GetEnvironmentVariable("HERMES_INTEGRATION_TEST_EXIT") == "1";

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("          HERMES INTEGRATION TEST APPLICATION");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Integration Test Mode: {isIntegrationTest}");
        Console.WriteLine($"  Auto Exit: {autoExit}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        HermesWindow.Prewarm();

        var builder = HermesBlazorAppBuilder.CreateDefault(args);

        builder.ConfigureWindow(options =>
        {
            options.Title = "Hermes Integration Tests";
            options.Width = 1280;
            options.Height = 800;
            options.CenterOnScreen = true;
            options.DevToolsEnabled = !isIntegrationTest;
            options.CustomTitleBar = true;
        });

        builder.Services.AddSingleton(new TestContext
        {
            IsIntegrationTest = isIntegrationTest,
            AutoExit = autoExit
        });

        builder.Services.AddFluentUIComponents();

        builder.RootComponents.Add<App>("#app");

        var app = builder.Build();

        app.MainWindow.MenuBar
            .AddMenu("File", file =>
            {
                file.AddItem("New", "file.new", item => item.WithAccelerator("Ctrl+N"))
                    .AddItem("Open...", "file.open", item => item.WithAccelerator("Ctrl+O"))
                    .AddSeparator()
                    .AddItem("Exit", "file.exit", item => item.WithAccelerator("Alt+F4"));
            })
            .AddMenu("Edit", edit =>
            {
                edit.AddItem("Undo", "edit.undo", item => item.WithAccelerator("Ctrl+Z"))
                    .AddItem("Redo", "edit.redo", item => item.WithAccelerator("Ctrl+Y"))
                    .AddSeparator()
                    .AddItem("Cut", "edit.cut", item => item.WithAccelerator("Ctrl+X"))
                    .AddItem("Copy", "edit.copy", item => item.WithAccelerator("Ctrl+C"))
                    .AddItem("Paste", "edit.paste", item => item.WithAccelerator("Ctrl+V"));
            })
            .AddMenu("Help", help =>
            {
                help.AddItem("About", "help.about");
            });

        app.MainWindow.MenuBar.ItemClicked += itemId =>
        {
            Console.WriteLine($"Menu item clicked: {itemId}");
            if (itemId == "file.exit")
            {
                app.MainWindow.Close();
            }
        };

        if (isIntegrationTest)
        {
            TestReporter.Ready();

            var runner = new ScenarioRunner(app, autoExit);
            _ = Task.Run(async () =>
            {
                // Wait for message loop to start pumping and WebView to begin initializing
                // On Windows CI, WebView2 initialization can be slow
                await Task.Delay(3000);
                await runner.RunAllScenariosAsync();
            });
        }

        app.Run();

        app.DisposeAsync().AsTask().GetAwaiter().GetResult();

        Console.WriteLine("Application closed.");
    }
}

/// <summary>
/// Context for test scenarios.
/// </summary>
public sealed class TestContext
{
    public bool IsIntegrationTest { get; init; }
    public bool AutoExit { get; init; }
}
