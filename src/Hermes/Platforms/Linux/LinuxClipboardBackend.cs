// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using System.Text;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxClipboardBackend : IClipboardBackend
{
    private readonly Func<bool> _isReady;

    public LinuxClipboardBackend()
        : this(LinuxNative.ClipboardIsReady)
    {
    }

    internal LinuxClipboardBackend(Func<bool> isReady) => _isReady = isReady;

    public bool IsSupported => true;

    public string? UnsupportedReason => null;

    // Static so the delegate outlives every GTK signal connection that may still call it.
    private static readonly LinuxNativeDelegates.ClipboardChangedCallback s_changedCallback = OnNativeChanged;
    private static LinuxClipboardBackend? s_monitoring;

    public event Action? Changed;

    public HermesDataFormats GetFormats() => LinuxClipboardTargets.ToFormats(GetTargets());

    public bool HasCustom(string mimeType) => GetTargets().Contains(mimeType, StringComparer.OrdinalIgnoreCase);

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes)
    {
        EnsureReady();

        string? text = null;
        string? html = null;
        ReadOnlyMemory<byte>? png = null;
        IReadOnlyList<string>? files = null;

        if (formats.HasFlag(HermesDataFormats.Text))
            text = TakeString(LinuxNative.ClipboardReadText());

        if (formats.HasFlag(HermesDataFormats.Html) && ReadContents(LinuxClipboardTargets.Html) is { } htmlBytes)
            html = LinuxClipboardTargets.DecodeHtml(htmlBytes);

        if (formats.HasFlag(HermesDataFormats.Png))
        {
            var pngBytes = ReadContents(LinuxClipboardTargets.Png);
            if (pngBytes is null && !GetFormats().HasFlag(HermesDataFormats.Files))
                pngBytes = TakeBytes(LinuxNative.ClipboardReadImageAsPng(out var length), length);
            if (pngBytes is not null)
                png = new ReadOnlyMemory<byte>(pngBytes);
        }

        Uri? url = null;
        if (formats.HasFlag(HermesDataFormats.Files) || formats.HasFlag(HermesDataFormats.Url))
        {
            var uriList = ReadContents(LinuxClipboardTargets.UriList) is { } uriBytes ? Encoding.UTF8.GetString(uriBytes) : null;

            if (formats.HasFlag(HermesDataFormats.Files))
            {
                if (uriList is not null)
                    files = FileUriList.ParseUriList(uriList);
                else if (ReadContents(LinuxClipboardTargets.GnomeCopiedFiles) is { } gnomeBytes)
                    files = FileUriList.ParseGnomeCopiedFiles(Encoding.UTF8.GetString(gnomeBytes));
                if (files is { Count: 0 })
                    files = null;
            }

            if (formats.HasFlag(HermesDataFormats.Url))
            {
                url = uriList is null ? null : FileUriList.FirstUrl(uriList);
                if (url is null && ReadContents(LinuxClipboardTargets.NetscapeUrl) is { } netscapeBytes)
                    url = LinuxClipboardTargets.ParseNetscapeUrl(netscapeBytes);
            }
        }

        var custom = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        if (customTypes.Count > 0)
        {
            // X11 target names are case-sensitive, so read each type under the name the owner offered.
            var targets = GetTargets();
            foreach (var mimeType in customTypes)
            {
                var target = targets.Contains(mimeType)
                    ? mimeType
                    : targets.FirstOrDefault(t => t.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
                if (target is not null && ReadContents(target) is { } data)
                    custom[mimeType] = data;
            }
        }

        return new HermesDataPackage { Text = text, Html = html, Png = png, Files = files, Url = url, Custom = custom };
    }

    public unsafe void Write(HermesDataPackage package)
    {
        EnsureReady();

        var entries = LinuxClipboardTargets.ForWrite(package);
        var count = entries.Count;
        var targets = (byte**)NativeMemory.AllocZeroed((nuint)Math.Max(count, 1), (nuint)sizeof(byte*));
        var data = (byte**)NativeMemory.AllocZeroed((nuint)Math.Max(count, 1), (nuint)sizeof(byte*));
        var lengths = (int*)NativeMemory.AllocZeroed((nuint)Math.Max(count, 1), sizeof(int));

        try
        {
            for (var i = 0; i < count; i++)
            {
                var (target, bytes) = entries[i];
                targets[i] = Utf8StringMarshaller.ConvertToUnmanaged(target);
                data[i] = (byte*)NativeMemory.Alloc((nuint)Math.Max(bytes.Length, 1));
                bytes.CopyTo(new Span<byte>(data[i], bytes.Length));
                lengths[i] = bytes.Length;
            }

            if (!LinuxNative.ClipboardWrite(targets, data, lengths, count))
                throw new IOException("The system clipboard rejected the write.");
        }
        finally
        {
            for (var i = 0; i < count; i++)
            {
                Utf8StringMarshaller.Free(targets[i]);
                NativeMemory.Free(data[i]);
            }
            NativeMemory.Free(targets);
            NativeMemory.Free(data);
            NativeMemory.Free(lengths);
        }
    }

    public void Clear()
    {
        EnsureReady();
        LinuxNative.ClipboardClear();
    }

    public void StartMonitoring()
    {
        EnsureReady();
        s_monitoring = this;
        LinuxNative.ClipboardStartMonitoring(Marshal.GetFunctionPointerForDelegate(s_changedCallback));
    }

    public void StopMonitoring()
    {
        LinuxNative.ClipboardStopMonitoring();
        s_monitoring = null;
    }

    public void Dispose()
    {
        if (ReferenceEquals(s_monitoring, this))
            StopMonitoring();
    }

    private static void OnNativeChanged() => s_monitoring?.Changed?.Invoke();

    private void EnsureReady()
    {
        if (!_isReady())
            throw new InvalidOperationException("The Linux clipboard is available once a Hermes window has initialized GTK.");
    }

    private IReadOnlyList<string> GetTargets()
    {
        EnsureReady();
        var array = LinuxNative.ClipboardGetTargets(out var count);
        if (array == IntPtr.Zero)
            return [];

        try
        {
            var targets = new string[count];
            for (var i = 0; i < count; i++)
                targets[i] = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(array, i * IntPtr.Size))!;
            return targets;
        }
        finally
        {
            LinuxNative.FreeStringArray(array, count);
        }
    }

    private static byte[]? ReadContents(string target) =>
        TakeBytes(LinuxNative.ClipboardReadContents(target, out var length), length);

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
            LinuxNative.Free(pointer);
        }
    }

    private static byte[]? TakeBytes(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero)
            return null;

        try
        {
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return bytes;
        }
        finally
        {
            LinuxNative.Free(pointer);
        }
    }
}
