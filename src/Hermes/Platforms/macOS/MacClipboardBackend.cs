// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.Platforms.macOS;

/// <summary>Text-only clipboard through pbcopy and pbpaste until the NSPasteboard backend lands.</summary>
[SupportedOSPlatform("macos")]
internal sealed class MacClipboardBackend : IClipboardBackend
{
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
            throw new NotSupportedException("Only text can be written to the clipboard on macOS in this version.");

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
        using var process = Process.Start(new ProcessStartInfo("pbcopy")
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
        })!;
        process.StandardInput.Write(text);
        process.StandardInput.Close();
        process.WaitForExit();
    }

    private static string ReadText()
    {
        using var process = Process.Start(new ProcessStartInfo("pbpaste")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        var result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}
