// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Graphics.Imaging;
using Windows.Win32.System.Com;

namespace Hermes.Platforms.Windows;

/// <summary>Decodes PNG to 32-bit BGRA through WIC, for the legacy bitmap written beside clipboard PNGs.</summary>
[SupportedOSPlatform("windows")]
internal static class WicPngDecoder
{
    private static readonly Guid s_wicImagingFactoryClsid = new("cacaf262-9370-4615-a13b-9f5539da4c0a");

    /// <summary>Returns false for anything WIC cannot decode, or whose pixels would exceed <paramref name="maxByteCount"/>.</summary>
    public static unsafe bool TryDecodeToBgra(ReadOnlySpan<byte> png, long maxByteCount, out int width, out int height, out byte[] bgra)
    {
        width = 0;
        height = 0;
        bgra = [];

        // Clipboard writes can come from any thread, and WIC needs COM initialized on the calling one.
        var comInit = PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED);
        byte[]? pinnedPng = null;
        IWICImagingFactory? factory = null;
        IWICStream? stream = null;
        IWICBitmapDecoder? decoder = null;
        IWICBitmapFrameDecode? frame = null;
        IWICFormatConverter? converter = null;

        try
        {
            var clsid = s_wicImagingFactoryClsid;
            var iid = typeof(IWICImagingFactory).GUID;
            if (PInvoke.CoCreateInstance(in clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, in iid, out var factoryObject).Failed)
                return false;
            factory = (IWICImagingFactory)factoryObject;

            // InitializeFromMemory keeps the pointer instead of copying, so the bytes stay pinned until the stream is released.
            pinnedPng = GC.AllocateUninitializedArray<byte>(png.Length, pinned: true);
            png.CopyTo(pinnedPng);

            factory.CreateStream(out stream);
            stream.InitializeFromMemory(pinnedPng, (uint)pinnedPng.Length);
            decoder = factory.CreateDecoderFromStream(stream, null, WICDecodeOptions.WICDecodeMetadataCacheOnLoad);
            decoder.GetFrame(0, out frame);

            factory.CreateFormatConverter(out converter);
            var bgraFormat = PInvoke.GUID_WICPixelFormat32bppBGRA;
            converter.Initialize(frame, &bgraFormat, WICBitmapDitherType.WICBitmapDitherTypeNone,
                null, 0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom);

            converter.GetSize(out var decodedWidth, out var decodedHeight);
            var byteCount = (long)decodedWidth * decodedHeight * 4;
            if (byteCount == 0 || byteCount > Math.Min(maxByteCount, Array.MaxLength))
                return false;

            var pixels = new byte[byteCount];
            converter.CopyPixels(null, decodedWidth * 4, (uint)byteCount, pixels);

            width = (int)decodedWidth;
            height = (int)decodedHeight;
            bgra = pixels;
            return true;
        }
        catch (Exception)
        {
            // WIC HRESULTs surface as COMException, ArgumentException or OutOfMemoryException; any of them just means no legacy bitmap.
            return false;
        }
        finally
        {
            Release(converter);
            Release(frame);
            Release(decoder);
            Release(stream);
            Release(factory);
            GC.KeepAlive(pinnedPng);
            if (comInit.Succeeded)
                PInvoke.CoUninitialize();
        }
    }

    private static void Release(object? comObject)
    {
        if (comObject is not null)
            Marshal.ReleaseComObject(comObject);
    }
}
