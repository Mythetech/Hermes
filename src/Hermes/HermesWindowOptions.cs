// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes;

/// <summary>
/// Configuration options for a Hermes window.
/// Set these properties before calling Show() or WaitForClose().
/// </summary>
public sealed class HermesWindowOptions
{
    /// <summary>
    /// Window title displayed in the title bar.
    /// </summary>
    public string Title { get; set; } = "Hermes Window";

    /// <summary>
    /// Initial window width in pixels.
    /// </summary>
    public int Width { get; set; } = 800;

    /// <summary>
    /// Initial window height in pixels.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    /// Initial X position in screen coordinates. Null for OS default or center.
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    /// Initial Y position in screen coordinates. Null for OS default or center.
    /// </summary>
    public int? Y { get; set; }

    /// <summary>
    /// Center the window on screen when shown. Overrides X/Y if true.
    /// </summary>
    public bool CenterOnScreen { get; set; } = true;

    /// <summary>
    /// Whether the window can be resized by the user.
    /// </summary>
    public bool Resizable { get; set; } = true;

    /// <summary>
    /// Remove the window title bar and borders (chromeless/borderless window).
    /// </summary>
    public bool Chromeless { get; set; }

    /// <summary>
    /// Start the window maximized.
    /// </summary>
    public bool Maximized { get; set; }

    /// <summary>
    /// Start the window minimized.
    /// </summary>
    public bool Minimized { get; set; }

    /// <summary>
    /// URL to load in the WebView on startup.
    /// </summary>
    public string? StartUrl { get; set; }

    /// <summary>
    /// HTML content to load directly in the WebView on startup.
    /// Ignored if StartUrl is set.
    /// </summary>
    public string? StartHtml { get; set; }

    /// <summary>
    /// Path to a window icon file (.ico on Windows, .png on other platforms).
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// Enable browser developer tools (F12).
    /// </summary>
    public bool DevToolsEnabled { get; set; }

    /// <summary>
    /// Enable the right-click context menu in the WebView.
    /// </summary>
    /// <remarks>
    /// <para>Default: <c>true</c></para>
    /// <para><b>Security Note:</b> For kiosk-mode or production applications,
    /// consider setting this to <c>false</c> to prevent access to "Inspect Element"
    /// or other browser menu items. See also <see cref="HermesBlazorAppBuilder.UseProductionDefaults"/>.</para>
    /// </remarks>
    public bool ContextMenuEnabled { get; set; } = true;

    /// <summary>
    /// Keep the window always on top of other windows.
    /// </summary>
    public bool TopMost { get; set; }

    /// <summary>
    /// Minimum window width in pixels.
    /// </summary>
    public int? MinWidth { get; set; }

    /// <summary>
    /// Minimum window height in pixels.
    /// </summary>
    public int? MinHeight { get; set; }

    /// <summary>
    /// Maximum window width in pixels.
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Maximum window height in pixels.
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Enable custom title bar mode.
    /// On macOS: Uses transparent title bar with native traffic light buttons, allowing
    /// WebView content to extend under the title bar area.
    /// On Windows: Enables DWM frame extension for fully custom window chrome.
    /// On Linux: Not supported. This flag is ignored and native GTK decorations are preserved.
    /// </summary>
    public bool CustomTitleBar { get; set; }

    /// <summary>
    /// Make the window background transparent, allowing the desktop or other windows
    /// to show through areas where the WebView content has no opaque background.
    /// Typically combined with <see cref="Chromeless"/> for frameless transparent overlays,
    /// or with CSS backdrop effects for glass/translucent UI.
    /// </summary>
    public bool Transparent { get; set; }

    /// <summary>
    /// Key used to persist window state. If set to non-null, window position, size, and
    /// maximized state are saved on close and restored on next launch.
    /// Use empty string to auto-derive key from window title.
    /// </summary>
    public string? WindowStateKey { get; set; }

    internal static void ApplyTo(HermesWindow window, HermesWindowOptions options)
    {
        window.SetTitle(options.Title);
        window.SetSize(options.Width, options.Height);

        if (options.X.HasValue && options.Y.HasValue)
            window.SetPosition(options.X.Value, options.Y.Value);

        if (options.CenterOnScreen)
            window.Center();

        window.SetResizable(options.Resizable);
        window.SetChromeless(options.Chromeless);
        window.SetTopMost(options.TopMost);
        window.SetDevToolsEnabled(options.DevToolsEnabled);
        window.SetContextMenuEnabled(options.ContextMenuEnabled);
        window.SetCustomTitleBar(options.CustomTitleBar);
        window.SetTransparent(options.Transparent);

        if (options.Maximized)
            window.Maximize();
        if (options.Minimized)
            window.Minimize();

        if (!string.IsNullOrEmpty(options.IconPath))
            window.SetIcon(options.IconPath);

        if (options.MinWidth.HasValue || options.MinHeight.HasValue)
            window.SetMinSize(options.MinWidth ?? 0, options.MinHeight ?? 0);

        if (options.MaxWidth.HasValue || options.MaxHeight.HasValue)
            window.SetMaxSize(options.MaxWidth ?? int.MaxValue, options.MaxHeight ?? int.MaxValue);

        if (options.WindowStateKey is not null)
            window.RememberWindowState(string.IsNullOrEmpty(options.WindowStateKey) ? null : options.WindowStateKey);
    }
}
