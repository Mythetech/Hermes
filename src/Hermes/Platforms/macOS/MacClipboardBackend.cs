// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.Platforms.macOS;

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

    public HermesDataFormats GetFormats() => (HermesDataFormats)MacNative.ClipboardGetFormats();

    public bool HasCustom(string mimeType) => MacNative.ClipboardHasType(mimeType);

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes)
    {
        string? text = null;
        string? html = null;
        ReadOnlyMemory<byte>? png = null;
        IReadOnlyList<string>? files = null;

        if (formats.HasFlag(HermesDataFormats.Text))
            text = TakeString(MacNative.ClipboardReadText());
        if (formats.HasFlag(HermesDataFormats.Html))
            html = TakeString(MacNative.ClipboardReadHtml());
        if (formats.HasFlag(HermesDataFormats.Png))
            png = TakeBytes(MacNative.ClipboardReadPng(out var pngLength), pngLength);
        if (formats.HasFlag(HermesDataFormats.Files))
            files = TakeStrings(MacNative.ClipboardReadFiles(out var fileCount), fileCount);

        var custom = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mimeType in customTypes)
        {
            if (TakeBytes(MacNative.ClipboardReadCustom(mimeType, out var length), length) is { } data)
                custom[mimeType] = data;
        }

        return new HermesDataPackage { Text = text, Html = html, Png = png, Files = files, Custom = custom };
    }

    public unsafe void Write(HermesDataPackage package)
    {
        var files = package.Files ?? [];
        var custom = package.Custom.ToArray();
        var filePointers = (byte**)NativeMemory.AllocZeroed((nuint)Math.Max(files.Count, 1), (nuint)sizeof(byte*));
        var typePointers = (byte**)NativeMemory.AllocZeroed((nuint)Math.Max(custom.Length, 1), (nuint)sizeof(byte*));
        var dataPointers = (byte**)NativeMemory.AllocZeroed((nuint)Math.Max(custom.Length, 1), (nuint)sizeof(byte*));
        var lengths = (int*)NativeMemory.AllocZeroed((nuint)Math.Max(custom.Length, 1), sizeof(int));

        try
        {
            for (var i = 0; i < files.Count; i++)
                filePointers[i] = Utf8StringMarshaller.ConvertToUnmanaged(files[i]);

            for (var i = 0; i < custom.Length; i++)
            {
                var data = custom[i].Value.Span;
                typePointers[i] = Utf8StringMarshaller.ConvertToUnmanaged(custom[i].Key);
                dataPointers[i] = (byte*)NativeMemory.Alloc((nuint)Math.Max(data.Length, 1));
                data.CopyTo(new Span<byte>(dataPointers[i], data.Length));
                lengths[i] = data.Length;
            }

            ReadOnlySpan<byte> png = package.Png is { } pngMemory ? pngMemory.Span : default;
            fixed (byte* pngPointer = png)
            {
                var written = MacNative.ClipboardWrite(
                    package.Text, package.Html, pngPointer, png.Length,
                    filePointers, files.Count,
                    typePointers, dataPointers, lengths, custom.Length);
                if (!written)
                    throw new IOException("The system clipboard rejected the write.");
            }
        }
        finally
        {
            for (var i = 0; i < files.Count; i++)
                Utf8StringMarshaller.Free(filePointers[i]);
            for (var i = 0; i < custom.Length; i++)
            {
                Utf8StringMarshaller.Free(typePointers[i]);
                NativeMemory.Free(dataPointers[i]);
            }
            NativeMemory.Free(filePointers);
            NativeMemory.Free(typePointers);
            NativeMemory.Free(dataPointers);
            NativeMemory.Free(lengths);
        }
    }

    public void Clear() => MacNative.ClipboardClear();

    public void StartMonitoring()
    {
    }

    public void StopMonitoring()
    {
    }

    public void Dispose()
    {
    }

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
