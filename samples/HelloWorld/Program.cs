using Hermes;

Console.WriteLine("Starting Hermes HelloWorld sample...");

var window = new HermesWindow()
    .SetTitle("Hermes - Hello World")
    .SetSize(1024, 768)
    .Center()
    .SetDevToolsEnabled(true)
    .Load("https://example.com")
    .OnWebMessage(msg => Console.WriteLine($"Web message received: {msg}"))
    .OnClosing(() => Console.WriteLine("Window closing..."))
    .OnResized((w, h) => Console.WriteLine($"Window resized to {w}x{h}"))
    .OnMoved((x, y) => Console.WriteLine($"Window moved to ({x}, {y})"))
    .OnFocusIn(() => Console.WriteLine("Window focused"))
    .OnFocusOut(() => Console.WriteLine("Window lost focus"));

Console.WriteLine("Showing window...");
window.WaitForClose();

Console.WriteLine("Window closed. Goodbye!");
