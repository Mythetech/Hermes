// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;
using Hermes.DockMenu;
using Hermes.SingleInstance;
using Hermes.StatusIcon;

namespace Hermes;

/// <summary>
/// Provides access to application-level features that are not tied to a specific window.
/// </summary>
public static class HermesApplication
{
    private static NativeDockMenu? _dockMenu;
    private static readonly object _dockMenuLock = new();
    private static readonly List<NativeStatusIcon> _statusIcons = new();
    private static readonly object _statusIconsLock = new();

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
    /// Creates a new system tray icon. The icon must be configured and shown by calling Show().
    /// Multiple tray icons are supported; each has an independent lifecycle.
    /// Tray icons are automatically disposed on Shutdown().
    /// </summary>
    /// <returns>A new <see cref="NativeStatusIcon"/> ready for configuration.</returns>
    public static NativeStatusIcon CreateStatusIcon()
    {
        var backend = CreateStatusIconBackend();
        var icon = new NativeStatusIcon(backend);

        lock (_statusIconsLock)
        {
            _statusIcons.Add(icon);
        }

        return icon;
    }

    /// <summary>
    /// Shuts down application-level resources.
    /// Call this when the application is exiting to clean up native resources.
    /// </summary>
    public static void Shutdown()
    {
        lock (_statusIconsLock)
        {
            foreach (var icon in _statusIcons)
            {
                icon.Dispose();
            }
            _statusIcons.Clear();
        }

        lock (_dockMenuLock)
        {
            _dockMenu?.Dispose();
            _dockMenu = null;
        }
    }

    /// <summary>
    /// Enable crash interception for the application.
    /// Installs handlers for unhandled exceptions, unobserved task exceptions,
    /// and WebView process crashes. Wire <see cref="Diagnostics.HermesCrashInterceptor.OnCrash"/>
    /// to receive crash context.
    /// </summary>
    public static void EnableCrashInterception() => Diagnostics.HermesCrashInterceptor.Enable();

    /// <summary>
    /// Creates a single-instance guard for the application.
    /// Call this early in Main() before creating any windows.
    /// </summary>
    /// <param name="applicationId">
    /// A unique identifier for this application. Must contain only alphanumeric characters,
    /// hyphens, underscores, and dots.
    /// </param>
    /// <returns>
    /// A <see cref="SingleInstanceGuard"/> that should be disposed when the application exits.
    /// Check <see cref="SingleInstanceGuard.IsFirstInstance"/> to determine if this is the primary instance.
    /// </returns>
    /// <example>
    /// <code>
    /// using var guard = HermesApplication.SingleInstance("my-app");
    /// if (!guard.IsFirstInstance)
    /// {
    ///     guard.NotifyFirstInstance(args);
    ///     return;
    /// }
    /// // Continue with window creation...
    /// </code>
    /// </example>
    public static SingleInstanceGuard SingleInstance(string applicationId)
    {
        return new SingleInstanceGuard(applicationId);
    }

    /// <summary>
    /// Opens a URL in the default browser.
    /// Only http:// and https:// schemes are allowed.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <exception cref="ArgumentException">Thrown when the URL is null, empty, or uses a disallowed scheme.</exception>
    public static void OpenUrl(string url) => Opener.OpenUrl(url);

    /// <summary>
    /// Opens a file or directory in its default application.
    /// Directories are opened in the default file manager.
    /// </summary>
    /// <param name="path">The path to the file or directory to open.</param>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the path does not exist.</exception>
    public static void OpenFile(string path) => Opener.OpenFile(path);

    /// <summary>
    /// Reveals a file or directory in the platform's file manager.
    /// For files, the containing folder is opened and the file is selected (macOS and Windows).
    /// On Linux, the containing directory is opened without file selection.
    /// </summary>
    /// <param name="path">The path to the file or directory to reveal.</param>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the path does not exist.</exception>
    public static void RevealInFileManager(string path) => Opener.RevealInFileManager(path);

    private static IDockMenuBackend? CreateDockMenuBackend()
    {
#if MACOS
        if (OperatingSystem.IsMacOS())
            return new Platforms.macOS.MacDockMenuBackend();
#endif
        return null;
    }

    private static IStatusIconBackend CreateStatusIconBackend()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
            return new Platforms.Windows.WindowsStatusIconBackend();
#endif
#if MACOS
        if (OperatingSystem.IsMacOS())
            return new Platforms.macOS.MacStatusIconBackend();
#endif
#if LINUX
        if (OperatingSystem.IsLinux())
            return new Platforms.Linux.LinuxStatusIconBackend();
#endif
        throw new PlatformNotSupportedException("System tray icons are not yet supported on this platform.");
    }
}
