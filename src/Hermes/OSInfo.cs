using System.Globalization;
using System.Runtime.InteropServices;

namespace Hermes;

/// <summary>
/// Provides information about the operating system.
/// </summary>
public sealed record OSInfo
{
    private static OSInfo? _current;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the current operating system information.
    /// </summary>
    public static OSInfo Current
    {
        get
        {
            if (_current is null)
            {
                lock (_lock)
                {
                    _current ??= new OSInfo();
                }
            }
            return _current;
        }
    }

    private OSInfo()
    {
        Platform = GetPlatform();
        Version = GetVersion();
        Architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
        Locale = CultureInfo.CurrentCulture.Name;
    }

    /// <summary>
    /// Gets the operating system platform (Windows, macOS, or Linux).
    /// </summary>
    public string Platform { get; }

    /// <summary>
    /// Gets the operating system version string.
    /// Examples: "10.0.22631" (Windows), "14.3.1" (macOS), "Ubuntu 22.04" (Linux).
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the CPU architecture (x64, arm64, x86, etc.).
    /// </summary>
    public string Architecture { get; }

    /// <summary>
    /// Gets the system locale in BCP 47 format (e.g., "en-US", "de-DE").
    /// </summary>
    public string Locale { get; }

    private static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsLinux()) return "Linux";
        return "Unknown";
    }

    private static string GetVersion()
    {
        if (OperatingSystem.IsWindows())
        {
            // Environment.OSVersion gives clean version on Windows
            return Environment.OSVersion.Version.ToString();
        }

        if (OperatingSystem.IsMacOS())
        {
            // RuntimeInformation.OSDescription: "Darwin 23.3.0 Darwin Kernel Version..."
            // We want just the macOS version which we can get from OSVersion
            return Environment.OSVersion.Version.ToString();
        }

        if (OperatingSystem.IsLinux())
        {
            // Try to get a friendly name from /etc/os-release
            return GetLinuxDistroVersion() ?? Environment.OSVersion.Version.ToString();
        }

        return Environment.OSVersion.Version.ToString();
    }

    private static string? GetLinuxDistroVersion()
    {
        try
        {
            const string osReleasePath = "/etc/os-release";
            if (!File.Exists(osReleasePath))
                return null;

            var lines = File.ReadAllLines(osReleasePath);
            string? prettyName = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                {
                    prettyName = line["PRETTY_NAME=".Length..].Trim('"');
                    break;
                }
            }

            return prettyName;
        }
        catch
        {
            return null;
        }
    }
}
