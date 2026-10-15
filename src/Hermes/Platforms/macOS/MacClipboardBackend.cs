// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.Platforms.macOS;

[SupportedOSPlatform("macos")]
internal sealed class MacClipboardBackend : IClipboardBackend
{
    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    // Static so the delegate outlives every native timer that may still call it.
    private static readonly MacNativeDelegates.ClipboardChangedCallback s_changedCallback = OnNativeChanged;
    private static MacClipboardBackend? s_monitoring;

    public event Action? Changed;

    public HermesDataFormats GetFormats() => (HermesDataFormats)MacNative.ClipboardGetFormats();

    public bool HasCustom(string mimeType) => MacNative.ClipboardHasType(mimeType);

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes)
    {
        string? text = null;
        string? html = null;
        ReadOnlyMemory<byte>? png = null;
        IReadOnlyList<string>? files = null;
        Uri? url = null;

        if (formats.HasFlag(HermesDataFormats.Text))
            text = TakeString(MacNative.ClipboardReadText());
        if (formats.HasFlag(HermesDataFormats.Html))
            html = TakeString(MacNative.ClipboardReadHtml());
        if (formats.HasFlag(HermesDataFormats.Png))
            png = TakeBytes(MacNative.ClipboardReadPng(out var pngLength), pngLength);
        if (formats.HasFlag(HermesDataFormats.Files))
            files = TakeStrings(MacNative.ClipboardReadFiles(out var fileCount), fileCount);
        if (formats.HasFlag(HermesDataFormats.Url))
            url = MacNativePackage.ParseUrl(TakeString(MacNative.ClipboardReadUrl()));

        var custom = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mimeType in customTypes)
        {
            if (TakeBytes(MacNative.ClipboardReadCustom(mimeType, out var length), length) is { } data)
                custom[mimeType] = data;
        }

        return new HermesDataPackage { Text = text, Html = html, Png = png, Files = files, Url = url, Custom = custom };
    }

    public unsafe void Write(HermesDataPackage package)
    {
        var native = MacNativePackage.Allocate(package);
        try
        {
            if (!MacNative.ClipboardWrite(&native))
                throw new IOException("The system clipboard rejected the write.");
        }
        finally
        {
            MacNativePackage.Free(ref native);
        }
    }

    public void Clear() => MacNative.ClipboardClear();

    public void StartMonitoring()
    {
        s_monitoring = this;
        MacNative.ClipboardStartMonitoring(Marshal.GetFunctionPointerForDelegate(s_changedCallback));
    }

    public void StopMonitoring()
    {
        MacNative.ClipboardStopMonitoring();
        s_monitoring = null;
    }

    public void Dispose()
    {
        if (ReferenceEquals(s_monitoring, this))
            StopMonitoring();
    }

    private static void OnNativeChanged() => s_monitoring?.Changed?.Invoke();

    private static string? TakeString(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUTF8(pointer);
        }
        finally
        {
            MacNative.Free(pointer);
        }
    }

    private static ReadOnlyMemory<byte>? TakeBytes(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero)
            return null;

        try
        {
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return new ReadOnlyMemory<byte>(bytes);
        }
        finally
        {
            MacNative.Free(pointer);
        }
    }

    private static IReadOnlyList<string>? TakeStrings(IntPtr array, int count)
    {
        if (array == IntPtr.Zero)
            return null;

        try
        {
            var result = new string[count];
            for (var i = 0; i < count; i++)
                result[i] = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(array, i * IntPtr.Size))!;
            return result;
        }
        finally
        {
            MacNative.FreeStringArray(array, count);
        }
    }
}
