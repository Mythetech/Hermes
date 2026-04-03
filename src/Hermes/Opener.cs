// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics;

namespace Hermes;

/// <summary>
/// Opens files, folders, and URLs in their default applications.
/// </summary>
public static class Opener
{
    private static readonly HashSet<string> AllowedUrlSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https"
    };

    /// <summary>
    /// Opens a URL in the default browser.
    /// Only http:// and https:// schemes are allowed.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <exception cref="ArgumentException">Thrown when the URL is null, empty, or uses a disallowed scheme.</exception>
    public static void OpenUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));

        if (!AllowedUrlSchemes.Contains(uri.Scheme))
            throw new ArgumentException($"URL scheme '{uri.Scheme}' is not allowed. Only http and https are supported.", nameof(url));

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    /// <summary>
    /// Opens a file or directory in its default application.
    /// Directories are opened in the default file manager.
    /// </summary>
    /// <param name="path">The path to the file or directory to open.</param>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the path does not exist.</exception>
    public static void OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException($"The path does not exist: {path}", path);

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>
    /// Reveals a file or directory in the platform's file manager.
    /// For files, the containing folder is opened and the file is selected (macOS and Windows).
    /// On Linux, the containing directory is opened without file selection.
    /// For directories, the directory is opened directly.
    /// </summary>
    /// <param name="path">The path to the file or directory to reveal.</param>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the path does not exist.</exception>
    public static void RevealInFileManager(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var isFile = File.Exists(path);
        var isDirectory = Directory.Exists(path);

        if (!isFile && !isDirectory)
            throw new FileNotFoundException($"The path does not exist: {path}", path);

        if (OperatingSystem.IsMacOS())
        {
            if (isFile)
                Process.Start("open", ["-R", path]);
            else
                Process.Start("open", [path]);
        }
        else if (OperatingSystem.IsWindows())
        {
            if (isFile)
                Process.Start("explorer", ["/select,", path]);
            else
                Process.Start("explorer", [path]);
        }
        else if (OperatingSystem.IsLinux())
        {
            var target = isFile ? Path.GetDirectoryName(path)! : path;
            Process.Start("xdg-open", [target]);
        }
        else
        {
            throw new PlatformNotSupportedException("RevealInFileManager is not supported on this platform.");
        }
    }
}
