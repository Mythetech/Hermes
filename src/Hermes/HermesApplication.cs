// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;
using Hermes.DockMenu;

namespace Hermes;

/// <summary>
/// Provides access to application-level features that are not tied to a specific window.
/// </summary>
public static class HermesApplication
{
    private static NativeDockMenu? _dockMenu;
    private static readonly object _dockMenuLock = new();

    /// <summary>
    /// Gets information about the current operating system.
    /// </summary>
    public static OSInfo OSInfo => OSInfo.Current;

    /// <summary>
    /// Gets the application dock menu. macOS only; returns null on other platforms.
    /// The dock menu appears when right-clicking the application's dock icon.
    /// Custom items appear above the default macOS entries (Options, Show All Windows, Hide, Quit).
    /// </summary>
    /// <remarks>
    /// The dock menu is created lazily on first access and persists for the application lifetime.
    /// To customize the dock menu, add items using the fluent API:
    /// <code>
    /// if (HermesApplication.DockMenu is { } dockMenu)
    /// {
    ///     dockMenu
    ///         .AddItem("New Window", "new-window")
    ///         .AddSeparator()
    ///         .AddSubmenu("Recent Files", "recent", submenu =>
    ///         {
    ///             submenu.AddItem("file1.txt", "recent.file1");
    ///         });
    ///
    ///     dockMenu.ItemClicked += itemId =>
    ///     {
    ///         if (itemId == "new-window") OpenNewWindow();
    ///     };
    /// }
    /// </code>
    /// </remarks>
    public static NativeDockMenu? DockMenu
    {
        get
        {
            if (!OperatingSystem.IsMacOS())
                return null;

            if (_dockMenu is null)
            {
                lock (_dockMenuLock)
                {
                    if (_dockMenu is null)
                    {
                        var backend = CreateDockMenuBackend();
                        if (backend is not null)
                        {
                            _dockMenu = new NativeDockMenu(backend);
                        }
                    }
                }
            }

            return _dockMenu;
        }
    }

    /// <summary>
    /// Shuts down application-level resources.
    /// Call this when the application is exiting to clean up native resources.
    /// </summary>
    public static void Shutdown()
    {
        lock (_dockMenuLock)
        {
            _dockMenu?.Dispose();
            _dockMenu = null;
        }
    }

    private static IDockMenuBackend? CreateDockMenuBackend()
    {
#if MACOS
        if (OperatingSystem.IsMacOS())
            return new Platforms.macOS.MacDockMenuBackend();
#endif
        return null;
    }
}
