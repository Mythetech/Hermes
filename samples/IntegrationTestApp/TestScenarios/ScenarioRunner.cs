using Hermes;
using Hermes.Blazor;

namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// Runs integration test scenarios and reports results.
/// Scenarios test various Hermes features to ensure they work correctly.
/// </summary>
public sealed class ScenarioRunner : IDisposable
{
    private readonly HermesBlazorApp _app;
    private readonly bool _autoExit;
    private bool _disposed;

    public ScenarioRunner(HermesBlazorApp app, bool autoExit = false)
    {
        _app = app;
        _autoExit = autoExit;
    }

    /// <summary>
    /// Run all test scenarios.
    /// Call this after the app is initialized and shown.
    /// </summary>
    public async Task RunAllScenariosAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Starting integration test scenarios...");
        Console.WriteLine();

        // Window lifecycle tests
        RunWindowInitializationTest();
        RunWindowTitleTest();
        RunWindowSizeTest();

        // Menu tests
        RunMenuCreationTest();
        RunMenuAcceleratorTest();

        // Custom titlebar tests (will be validated by component)
        TestReporter.Start("custom-titlebar-rendered");

        // Wait for Blazor components to finish their tests
        // The FluentComponentsPage will report its own results
        await Task.Delay(2000);

        // Print summary
        TestReporter.PrintSummary();

        // Auto-exit if in CI mode
        if (_autoExit)
        {
            Console.WriteLine("Auto-exiting after test completion...");
            // Close must happen on the UI thread
            _app.MainWindow.Invoke(() => _app.MainWindow.Close());
        }
    }

    private void RunWindowInitializationTest()
    {
        TestReporter.Start("window-initialization");
        try
        {
            var window = _app.MainWindow;
            TestReporter.Assert("window-initialization",
                window != null,
                "MainWindow is null");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("window-initialization", ex.Message);
        }
    }

    private void RunWindowTitleTest()
    {
        TestReporter.Start("window-title");
        try
        {
            var expectedTitle = "Hermes Integration Tests";
            var actualTitle = _app.MainWindow.Title;
            TestReporter.Assert("window-title",
                actualTitle == expectedTitle,
                $"Expected '{expectedTitle}', got '{actualTitle}'");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("window-title", ex.Message);
        }
    }

    private void RunWindowSizeTest()
    {
        TestReporter.Start("window-size");
        try
        {
            var (width, height) = _app.MainWindow.Size;
            TestReporter.Assert("window-size",
                width > 0 && height > 0,
                $"Invalid size: {width}x{height}");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("window-size", ex.Message);
        }
    }

    private void RunMenuCreationTest()
    {
        TestReporter.Start("menu-creation");
        try
        {
            var menuBar = _app.MainWindow.MenuBar;
            TestReporter.Assert("menu-creation",
                menuBar != null,
                "MenuBar is null");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("menu-creation", ex.Message);
        }
    }

    private void RunMenuAcceleratorTest()
    {
        TestReporter.Start("menu-accelerator");
        try
        {
            // Menu modifications must happen on the UI thread (especially on macOS)
            // Use Invoke to marshal to the main thread
            Exception? invokeError = null;
            _app.MainWindow.Invoke(() =>
            {
                try
                {
                    var menuBar = _app.MainWindow.MenuBar;
                    menuBar.AddMenu("Test", test =>
                    {
                        test.AddItem("Test Item", "test.item", item =>
                            item.WithAccelerator("Ctrl+Shift+T"));
                    });
                }
                catch (Exception ex)
                {
                    invokeError = ex;
                }
            });

            if (invokeError != null)
                throw invokeError;

            TestReporter.Pass("menu-accelerator");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("menu-accelerator", ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
