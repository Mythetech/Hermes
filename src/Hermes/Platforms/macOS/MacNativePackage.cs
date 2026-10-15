// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Hermes.Contracts.DataTransfer;

namespace Hermes.Platforms.macOS;

/// <summary>
/// A data package laid out for the native library. Must match HermesNativePackage in HermesTypes.h.
/// </summary>
[SupportedOSPlatform("macos")]
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct HermesNativePackage
{
    public byte* Text;
    public byte* Html;
    public byte* Url;
    public byte* Png;
    public int PngLength;
    public byte** Files;
    public int FileCount;
    public byte** CustomTypes;
    public byte** CustomData;
    public int* CustomLengths;
    public int CustomCount;
}

/// <summary>
/// Copies <see cref="HermesDataPackage"/> to and from <see cref="HermesNativePackage"/>.
/// </summary>
[SupportedOSPlatform("macos")]
internal static unsafe class MacNativePackage
{
    /// <summary>Copies the package into native memory; release it with <see cref="Free"/>.</summary>
    public static HermesNativePackage Allocate(HermesDataPackage package)
    {
        var native = default(HermesNativePackage);
        try
        {
            native.Text = Utf8StringMarshaller.ConvertToUnmanaged(package.Text);
            native.Html = Utf8StringMarshaller.ConvertToUnmanaged(package.Html);
            native.Url = Utf8StringMarshaller.ConvertToUnmanaged(package.Url?.AbsoluteUri);

            if (package.Png is { } png)
            {
                native.Png = CopyBytes(png.Span);
                native.PngLength = png.Length;
            }

            if (package.Files is { Count: > 0 } files)
            {
                native.Files = (byte**)NativeMemory.AllocZeroed((nuint)files.Count, (nuint)sizeof(byte*));
                native.FileCount = files.Count;
                for (var i = 0; i < files.Count; i++)
                    native.Files[i] = Utf8StringMarshaller.ConvertToUnmanaged(files[i]);
            }

            if (package.Custom.Count > 0)
            {
                var custom = package.Custom.ToArray();
                native.CustomTypes = (byte**)NativeMemory.AllocZeroed((nuint)custom.Length, (nuint)sizeof(byte*));
                native.CustomData = (byte**)NativeMemory.AllocZeroed((nuint)custom.Length, (nuint)sizeof(byte*));
                native.CustomLengths = (int*)NativeMemory.AllocZeroed((nuint)custom.Length, sizeof(int));
                native.CustomCount = custom.Length;
                for (var i = 0; i < custom.Length; i++)
                {
                    native.CustomTypes[i] = Utf8StringMarshaller.ConvertToUnmanaged(custom[i].Key);
                    native.CustomData[i] = CopyBytes(custom[i].Value.Span);
                    native.CustomLengths[i] = custom[i].Value.Length;
                }
            }

            return native;
        }
        catch
        {
            Free(ref native);
            throw;
        }
    }

    /// <summary>Releases a package made by <see cref="Allocate"/> and clears it.</summary>
    public static void Free(ref HermesNativePackage native)
    {
        Utf8StringMarshaller.Free(native.Text);
        Utf8StringMarshaller.Free(native.Html);
        Utf8StringMarshaller.Free(native.Url);
        NativeMemory.Free(native.Png);

        if (native.Files is not null)
        {
            for (var i = 0; i < native.FileCount; i++)
                Utf8StringMarshaller.Free(native.Files[i]);
            NativeMemory.Free(native.Files);
        }

        for (var i = 0; i < native.CustomCount; i++)
        {
            if (native.CustomTypes is not null)
                Utf8StringMarshaller.Free(native.CustomTypes[i]);
            if (native.CustomData is not null)
                NativeMemory.Free(native.CustomData[i]);
        }
        NativeMemory.Free(native.CustomTypes);
        NativeMemory.Free(native.CustomData);
        NativeMemory.Free(native.CustomLengths);

        native = default;
    }

    /// <summary>Copies a native package into managed memory; the native memory stays with its owner.</summary>
    public static HermesDataPackage Read(in HermesNativePackage native)
    {
        List<string>? files = null;
        for (var i = 0; i < native.FileCount; i++)
        {
            if (Utf8StringMarshaller.ConvertToManaged(native.Files[i]) is { } file)
                (files ??= []).Add(file);
        }

        ReadOnlyMemory<byte>? png = null;
        if (native.Png is not null && native.PngLength > 0)
            png = new ReadOnlySpan<byte>(native.Png, native.PngLength).ToArray();

        var custom = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < native.CustomCount; i++)
        {
            if (Utf8StringMarshaller.ConvertToManaged(native.CustomTypes[i]) is { } mimeType && native.CustomData[i] is not null)
                custom[mimeType] = new ReadOnlySpan<byte>(native.CustomData[i], native.CustomLengths[i]).ToArray();
        }

        return new HermesDataPackage
        {
            Text = Utf8StringMarshaller.ConvertToManaged(native.Text),
            Html = Utf8StringMarshaller.ConvertToManaged(native.Html),
            Url = ParseUrl(Utf8StringMarshaller.ConvertToManaged(native.Url)),
            Png = png,
            Files = files,
            Custom = custom,
        };
    }

    /// <summary>The URL when it is absolute and not a file URL, otherwise null.</summary>
    public static Uri? ParseUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && !url.IsFile ? url : null;

    // At least one byte, so an empty custom format still has a pointer to hand over.
    private static byte* CopyBytes(ReadOnlySpan<byte> data)
    {
        var buffer = (byte*)NativeMemory.Alloc((nuint)Math.Max(data.Length, 1));
        data.CopyTo(new Span<byte>(buffer, data.Length));
        return buffer;
    }
}
