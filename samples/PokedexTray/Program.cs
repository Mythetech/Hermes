// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes;
using PokedexTray;

Console.WriteLine("Starting PokeDex Tray sample...");

using var service = new PokemonService();
var isVisible = false;
var window = LookupWindow.Create(service, onHidden: () => isVisible = false);

// Initialize the window (bootstraps the platform runtime on macOS) but keep it hidden.
// This must happen before creating the tray icon, as macOS requires NSApplication
// to be initialized before any status bar items can be created.
window.Show();
window.Hide();

// Position the window near the system tray rather than centered on screen.
// After centering, we can derive screen size from the centered position:
//   centered.X = (screenWidth - windowWidth) / 2
//   screenWidth = centered.X * 2 + windowWidth
var (cx, cy) = window.Position;
const int windowWidth = 360;
const int windowHeight = 500;
const int padding = 8;
var screenWidth = cx * 2 + windowWidth;
var screenHeight = cy * 2 + windowHeight;

if (OperatingSystem.IsMacOS())
{
    // macOS: tray is in the top menu bar, position top-right below the menu bar
    window.Position = (screenWidth - windowWidth - padding, 30);
}
else
{
    // Windows/Linux: tray is bottom-right, position above the taskbar
    window.Position = (screenWidth - windowWidth - padding, screenHeight - windowHeight - 60);
}

var trayIconPath = Path.Combine(AppContext.BaseDirectory, "pokeballTemplate.png");
if (HermesApplication.CreateStatusIcon() is { } tray)
{
    tray.SetIcon(trayIconPath)
        .SetTooltip("PokeDex Tray")
        .SetMenu(menu =>
        {
            menu.AddItem("PokeDex Tray", "tray.title", item => item.WithEnabled(false))
                .AddSeparator()
                .AddItem("Lookup", "tray.lookup")
                .AddItem("Clear Cache", "tray.clear")
                .AddSeparator()
                .AddItem("Quit", "tray.quit");
        })
        .OnClicked(() =>
        {
            if (isVisible)
            {
                window.Hide();
                isVisible = false;
            }
            else
            {
                window.Show();
                isVisible = true;
            }
        });

    tray.Menu!.ItemClicked += itemId =>
    {
        switch (itemId)
        {
            case "tray.lookup":
                if (!isVisible)
                {
                    window.Show();
                    isVisible = true;
                }
                break;
            case "tray.clear":
                service.ClearCache();
                Console.WriteLine("Cache cleared.");
                break;
            case "tray.quit":
                tray.Dispose();
                window.Close();
                break;
        }
    };

    tray.Show();
    Console.WriteLine("Tray icon active. Click to open the PokeDex lookup window.");
}
else
{
    Console.WriteLine("System tray not supported on this platform. Showing window directly.");
    window.Show();
}

window.OnClosing(() =>
{
    Console.WriteLine("Window closed. Shutting down...");
});

window.WaitForClose();
HermesApplication.Shutdown();
Console.WriteLine("Goodbye!");
