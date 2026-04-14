// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Reflection;
using System.Runtime.Versioning;

namespace Hermes;

/// <summary>
/// Registers or unregisters the application to launch at system startup/login.
/// </summary>
public static class Autostart
{
    /// <summary>
    /// Enables or disables autostart for this application.
    /// The app ID is derived from the entry assembly name.
    /// </summary>
    /// <param name="enabled">Whether to enable or disable autostart.</param>
    /// <param name="args">Optional command-line arguments to pass when launched at login.</param>
    public static void SetEnabled(bool enabled, string[]? args = null)
    {
        var appId = Assembly.GetEntryAssembly()?.GetName().Name
            ?? throw new InvalidOperationException("Could not determine application name from the entry assembly.");

        SetEnabled(appId, enabled, args);
    }

    /// <summary>
    /// Enables or disables autostart with an explicit app ID.
    /// </summary>
    /// <param name="appId">A unique identifier for this application.</param>
    /// <param name="enabled">Whether to enable or disable autostart.</param>
    /// <param name="args">Optional command-line arguments to pass when launched at login.</param>
    public static void SetEnabled(string appId, bool enabled, string[]? args = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the executable path.");

        if (enabled)
            Enable(appId, executablePath, args);
        else
            Disable(appId);
    }

    /// <summary>
    /// Whether autostart is currently enabled, using the app ID derived from the entry assembly name.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            var appId = Assembly.GetEntryAssembly()?.GetName().Name
                ?? throw new InvalidOperationException("Could not determine application name from the entry assembly.");

            return GetIsEnabled(appId);
        }
    }

    /// <summary>
    /// Whether autostart is currently enabled for the given app ID.
    /// </summary>
    /// <param name="appId">The application identifier to check.</param>
    public static bool GetIsEnabled(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        if (OperatingSystem.IsMacOS())
            return GetIsEnabledMacOS(appId);
        else if (OperatingSystem.IsWindows())
            return GetIsEnabledWindows(appId);
        else if (OperatingSystem.IsLinux())
            return GetIsEnabledLinux(appId);

        throw new PlatformNotSupportedException("Autostart is not supported on this platform.");
    }

    private static void Enable(string appId, string executablePath, string[]? args)
    {
        if (OperatingSystem.IsMacOS())
            EnableMacOS(appId, executablePath, args);
        else if (OperatingSystem.IsWindows())
            EnableWindows(appId, executablePath, args);
        else if (OperatingSystem.IsLinux())
            EnableLinux(appId, executablePath, args);
        else
            throw new PlatformNotSupportedException("Autostart is not supported on this platform.");
    }

    private static void Disable(string appId)
    {
        if (OperatingSystem.IsMacOS())
            DisableMacOS(appId);
        else if (OperatingSystem.IsWindows())
            DisableWindows(appId);
        else if (OperatingSystem.IsLinux())
            DisableLinux(appId);
        else
            throw new PlatformNotSupportedException("Autostart is not supported on this platform.");
    }

    // --- macOS (LaunchAgent plist) ---

    [SupportedOSPlatform("macos")]
    private static void EnableMacOS(string appId, string executablePath, string[]? args)
    {
        var plistPath = GetMacOSPlistPath(appId);

        var programArgs = $"        <string>{executablePath}</string>";
        if (args is { Length: > 0 })
        {
            programArgs += Environment.NewLine +
                string.Join(Environment.NewLine, args.Select(a => $"        <string>{a}</string>"));
        }

        var plist = $"""
    <?xml version="1.0" encoding="UTF-8"?>
    <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
    <plist version="1.0">
    <dict>
        <key>Label</key>
        <string>{appId}</string>
        <key>ProgramArguments</key>
        <array>
    {programArgs}
        </array>
        <key>RunAtLoad</key>
        <true/>
    </dict>
    </plist>
    """;

        File.WriteAllText(plistPath, plist);
    }

    [SupportedOSPlatform("macos")]
    private static void DisableMacOS(string appId)
    {
        var plistPath = GetMacOSPlistPath(appId);

        if (File.Exists(plistPath))
            File.Delete(plistPath);
    }

    [SupportedOSPlatform("macos")]
    private static bool GetIsEnabledMacOS(string appId)
    {
        return File.Exists(GetMacOSPlistPath(appId));
    }

    [SupportedOSPlatform("macos")]
    private static string GetMacOSPlistPath(string appId)
    {
        var launchAgentsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents");

        return Path.Combine(launchAgentsDir, $"{appId}.plist");
    }

    // --- Windows (Registry) ---

#if WINDOWS
    [SupportedOSPlatform("windows")]
    private static void EnableWindows(string appId, string executablePath, string[]? args)
    {
        var value = args is { Length: > 0 }
            ? $"\"{executablePath}\" {string.Join(" ", args)}"
            : $"\"{executablePath}\"";

        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            writable: true);

        key?.SetValue(appId, value);
    }

    [SupportedOSPlatform("windows")]
    private static void DisableWindows(string appId)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            writable: true);

        key?.DeleteValue(appId, throwOnMissingValue: false);
    }

    [SupportedOSPlatform("windows")]
    private static bool GetIsEnabledWindows(string appId)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");

        return key?.GetValue(appId) is not null;
    }
#else
    [SupportedOSPlatform("windows")]
    private static void EnableWindows(string appId, string executablePath, string[]? args) => throw new PlatformNotSupportedException();

    [SupportedOSPlatform("windows")]
    private static void DisableWindows(string appId) => throw new PlatformNotSupportedException();

    [SupportedOSPlatform("windows")]
    private static bool GetIsEnabledWindows(string appId) => throw new PlatformNotSupportedException();
#endif

    // --- Linux (XDG Desktop Entry) ---

    [SupportedOSPlatform("linux")]
    private static void EnableLinux(string appId, string executablePath, string[]? args)
    {
        var autostartDir = GetLinuxAutostartDir();
        Directory.CreateDirectory(autostartDir);

        var execLine = args is { Length: > 0 }
            ? $"{executablePath} {string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}"
            : executablePath;

        var desktopEntry = $"""
    [Desktop Entry]
    Type=Application
    Name={appId}
    Exec={execLine}
    X-GNOME-Autostart-enabled=true
    """;

        var desktopPath = Path.Combine(autostartDir, $"{appId}.desktop");
        File.WriteAllText(desktopPath, desktopEntry);
    }

    [SupportedOSPlatform("linux")]
    private static void DisableLinux(string appId)
    {
        var desktopPath = Path.Combine(GetLinuxAutostartDir(), $"{appId}.desktop");

        if (File.Exists(desktopPath))
            File.Delete(desktopPath);
    }

    [SupportedOSPlatform("linux")]
    private static bool GetIsEnabledLinux(string appId)
    {
        return File.Exists(Path.Combine(GetLinuxAutostartDir(), $"{appId}.desktop"));
    }

    [SupportedOSPlatform("linux")]
    private static string GetLinuxAutostartDir()
    {
        var configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(configDir, "autostart");
    }
}
