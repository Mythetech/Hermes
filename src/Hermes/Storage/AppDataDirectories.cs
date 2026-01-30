using System.Reflection;

namespace Hermes.Storage;

/// <summary>
/// Provides cross-platform application data directory resolution.
/// </summary>
public static class AppDataDirectories
{
    /// <summary>
    /// Gets the path for user-specific application data.
    /// Windows: %LOCALAPPDATA%\Hermes\{subdirectory}
    /// macOS: ~/Library/Application Support/Hermes/{subdirectory}
    /// Linux: ~/.local/share/Hermes/{subdirectory}
    /// </summary>
    /// <param name="subdirectory">Subdirectory within the Hermes data folder.</param>
    /// <returns>The full path to the user data directory.</returns>
    public static string GetUserDataPath(string subdirectory)
    {
        string basePath;

        if (OperatingSystem.IsWindows())
        {
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // ~/Library/Application Support
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else if (OperatingSystem.IsLinux())
        {
            // XDG_DATA_HOME or ~/.local/share
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            basePath = !string.IsNullOrEmpty(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
        else
        {
            // Fallback for unknown platforms
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(basePath, "Hermes", subdirectory);
    }

    /// <summary>
    /// Gets the current application name from the entry assembly.
    /// Falls back to "HermesApp" if unavailable.
    /// </summary>
    /// <returns>The application name.</returns>
    public static string GetApplicationName()
    {
        var assembly = Assembly.GetEntryAssembly();
        var name = assembly?.GetName().Name;

        return !string.IsNullOrEmpty(name) ? name : "HermesApp";
    }
}
