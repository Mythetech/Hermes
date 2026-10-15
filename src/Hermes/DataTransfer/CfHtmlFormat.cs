// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Globalization;
using System.Text;

namespace Hermes.DataTransfer;

/// <summary>
/// The Windows "HTML Format" clipboard encoding. Offsets in its header count UTF-8 bytes.
/// </summary>
internal static class CfHtmlFormat
{
    private const string StartMarker = "<!--StartFragment-->";
    private const string EndMarker = "<!--EndFragment-->";
    private const string Prefix = "<html><body>\r\n" + StartMarker;
    private const string Suffix = EndMarker + "\r\n</body></html>";

    public static byte[] Build(string fragment)
    {
        var headerLength = Encoding.UTF8.GetByteCount(Header(0, 0, 0, 0));
        var startFragment = headerLength + Encoding.UTF8.GetByteCount(Prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(Suffix);

        return Encoding.UTF8.GetBytes(Header(headerLength, endHtml, startFragment, endFragment) + Prefix + fragment + Suffix);
    }

    public static string? Parse(ReadOnlySpan<byte> data)
    {
        var nul = data.IndexOf((byte)0);
        if (nul >= 0)
            data = data[..nul];

        var startFragment = ReadOffset(data, "StartFragment:");
        var endFragment = ReadOffset(data, "EndFragment:");
        if (startFragment >= 0 && endFragment >= startFragment && endFragment <= data.Length)
            return Encoding.UTF8.GetString(data[startFragment..endFragment]);

        var text = Encoding.UTF8.GetString(data);
        var start = text.IndexOf(StartMarker, StringComparison.OrdinalIgnoreCase);
        var end = text.IndexOf(EndMarker, StringComparison.OrdinalIgnoreCase);
        if (start >= 0 && end > start)
            return text[(start + StartMarker.Length)..end];

        var startHtml = ReadOffset(data, "StartHTML:");
        return startHtml >= 0 && startHtml <= data.Length ? Encoding.UTF8.GetString(data[startHtml..]) : null;
    }

    private static string Header(int startHtml, int endHtml, int startFragment, int endFragment) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Version:0.9\r\nStartHTML:{startHtml:D10}\r\nEndHTML:{endHtml:D10}\r\nStartFragment:{startFragment:D10}\r\nEndFragment:{endFragment:D10}\r\n");

    private static int ReadOffset(ReadOnlySpan<byte> data, string key)
    {
        var index = data.IndexOf(Encoding.ASCII.GetBytes(key));
        if (index < 0)
            return -1;

        var rest = data[(index + key.Length)..];
        var lineEnd = rest.IndexOfAny((byte)'\r', (byte)'\n');
        var value = Encoding.ASCII.GetString(lineEnd >= 0 ? rest[..lineEnd] : rest);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset) ? offset : -1;
    }
}
