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

    public event Action? Changed
    {
        add { }
        remove { }
    }

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

        if (formats.HasFlag(HermesDataFormats.Files))
        {
            if (ReadContents(LinuxClipboardTargets.UriList) is { } uriBytes)
                files = FileUriList.ParseUriList(Encoding.UTF8.GetString(uriBytes));
            else if (ReadContents(LinuxClipboardTargets.GnomeCopiedFiles) is { } gnomeBytes)
                files = FileUriList.ParseGnomeCopiedFiles(Encoding.UTF8.GetString(gnomeBytes));
            if (files is { Count: 0 })
                files = null;
        }

        var custom = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mimeType in customTypes)
        {
            if (ReadContents(mimeType) is { } data)
                custom[mimeType] = data;
        }

        return new HermesDataPackage { Text = text, Html = html, Png = png, Files = files, Custom = custom };
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
    }

    public void StopMonitoring()
    {
    }

    public void Dispose()
    {
    }

    private void EnsureReady()
    {
        if (!_isReady())
            throw new InvalidOperationException("The Linux clipboard is available once a Hermes window or application has initialized GTK.");
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
