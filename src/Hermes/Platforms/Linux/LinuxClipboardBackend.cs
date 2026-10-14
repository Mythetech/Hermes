// Copyright (c) Mythetech. Licensed under the MIT License.
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.Platforms.Linux;

/// <summary>Text-only clipboard through xclip until the GTK backend lands.</summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxClipboardBackend : IClipboardBackend
{
    private const string MissingXclip =
        "xclip is required for clipboard support on Linux. Install it with: sudo apt install xclip";

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public HermesDataFormats GetFormats() =>
        string.IsNullOrEmpty(ReadText()) ? HermesDataFormats.None : HermesDataFormats.Text;

    public bool HasCustom(string mimeType) => false;

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes)
    {
        if (!formats.HasFlag(HermesDataFormats.Text))
            return new HermesDataPackage();

        var text = ReadText();
        return new HermesDataPackage { Text = string.IsNullOrEmpty(text) ? null : text };
    }

    public void Write(HermesDataPackage package)
    {
        if (package.Formats != HermesDataFormats.Text || package.Custom.Count > 0)
            throw new NotSupportedException("Only text can be written to the clipboard on Linux in this version.");

        WriteText(package.Text!);
    }

    public void Clear() => WriteText(string.Empty);

    public void StartMonitoring()
    {
    }

    public void StopMonitoring()
    {
    }

    public void Dispose()
    {
    }

    private static void WriteText(string text)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("xclip")
            {
                Arguments = "-selection clipboard",
                RedirectStandardInput = true,
                UseShellExecute = false,
            })!;
            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit();
        }
        catch (Win32Exception)
        {
            throw new PlatformNotSupportedException(MissingXclip);
        }
    }

    private static string ReadText()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("xclip")
            {
                Arguments = "-selection clipboard -o",
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            var result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
        catch (Win32Exception)
        {
            throw new PlatformNotSupportedException(MissingXclip);
        }
    }
}
