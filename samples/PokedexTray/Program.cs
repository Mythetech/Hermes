// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes;
using PokedexTray;

Console.WriteLine("Starting PokeDex Tray sample...");

HermesApplication.SetAccessoryMode();

using var service = new PokemonService();
var isVisible = false;
var window = LookupWindow.Create(service, onHidden: () => isVisible = false);

// Initialize the window (bootstraps the platform runtime on macOS) but keep it hidden.
// This must happen before creating the tray icon, as macOS requires NSApplication
// to be initialized before any status bar items can be created.
window.Show();
window.Hide();

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
                PositionWindowUnderTray(window, tray);
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
                    PositionWindowUnderTray(window, tray);
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

    // Pre-position the window under the tray icon so the initial Show()/Hide()
    // bootstrap doesn't leave it centered on the wrong monitor.
    PositionWindowUnderTray(window, tray);

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

void PositionWindowUnderTray(HermesWindow w, Hermes.StatusIcon.NativeStatusIcon trayIcon)
{
    var (ix, iy, iw, ih) = trayIcon.GetScreenPosition();
    if (ix == 0 && iy == 0 && iw == 0 && ih == 0)
        return; // Position unknown, leave window where it is

    // Center the window horizontally under the tray icon, directly below it
    const int windowWidth = 360;
    int x = ix + (iw / 2) - (windowWidth / 2);
    int y = iy + ih;

    w.Position = (x, y);
}
