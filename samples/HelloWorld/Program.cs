// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes;

namespace HelloWorld;

internal static class Program
{
    // Windows requires the main thread to be STA for WebView2; the attribute
    // only works on an explicit synchronous Main, never top-level statements.
    [STAThread]
    private static void Main(string[] args)
    {
        Console.WriteLine("Starting Hermes HelloWorld sample...");

        var window = new HermesWindow()
            .SetTitle("Hermes - Hello World")
            .SetSize(1024, 768)
            .Center()
            .SetDevToolsEnabled(true)
            .Load("https://docs.mythetech.com/smoke-test")
            .OnWebMessage(msg => Console.WriteLine($"Web message received: {msg}"))
            .OnClosing(() => Console.WriteLine("Window closing..."))
            .OnResized((w, h) => Console.WriteLine($"Window resized to {w}x{h}"))
            .OnMoved((x, y) => Console.WriteLine($"Window moved to ({x}, {y})"))
            .OnFocusIn(() => Console.WriteLine("Window focused"))
            .OnFocusOut(() => Console.WriteLine("Window lost focus"));

        Console.WriteLine("Showing window...");
        window.WaitForClose();

        Console.WriteLine("Window closed. Goodbye!");
    }
}
