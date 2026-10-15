// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Hermes.Contracts.DataTransfer;

namespace Hermes.DataTransfer;

/// <summary>
/// Maps X11 and Wayland clipboard targets to Hermes formats and back.
/// </summary>
internal static class LinuxClipboardTargets
{
    public const string Html = "text/html";
    public const string Png = "image/png";
    public const string UriList = "text/uri-list";
    public const string GnomeCopiedFiles = "x-special/gnome-copied-files";
    public const string NetscapeUrl = "_NETSCAPE_URL";

    private static readonly HashSet<string> s_textTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "UTF8_STRING",
        "text/plain;charset=utf-8",
        "text/plain",
        "STRING",
        "TEXT",
        "COMPOUND_TEXT",
    };

    public static HermesDataFormats ToFormats(IReadOnlyList<string> targets)
    {
        var formats = HermesDataFormats.None;
        var link = targets.Contains(NetscapeUrl);
        // A uri-list offered beside a link target holds that link, so it alone does not mean files.
        var files = targets.Contains(GnomeCopiedFiles) || (targets.Contains(UriList) && !link);

        if (targets.Any(s_textTargets.Contains))
            formats |= HermesDataFormats.Text;
        if (targets.Contains(Html))
            formats |= HermesDataFormats.Html;
        if (files)
            formats |= HermesDataFormats.Files;
        // File managers add an icon image beside copied files; only a real PNG counts then.
        if (targets.Contains(Png) || (!files && targets.Any(t => t.StartsWith("image/", StringComparison.OrdinalIgnoreCase))))
            formats |= HermesDataFormats.Png;
        if (link)
            formats |= HermesDataFormats.Url;

        return formats;
    }

    public static IReadOnlyList<(string Target, byte[] Data)> ForWrite(HermesDataPackage package)
    {
        var entries = new List<(string Target, byte[] Data)>();

        if (package.Text is { } text)
        {
            var utf8 = Encoding.UTF8.GetBytes(text);
            entries.Add(("UTF8_STRING", utf8));
            entries.Add(("text/plain;charset=utf-8", utf8));
            entries.Add(("text/plain", utf8));
        }
        if (package.Html is { } html)
            entries.Add((Html, Encoding.UTF8.GetBytes(html)));
        if (package.Png is { } png)
            entries.Add((Png, png.ToArray()));
        if (package.Files is not null || package.Url is not null)
            entries.Add((UriList, Encoding.UTF8.GetBytes(FileUriList.BuildUriList(package.Files, package.Url))));
        if (package.Files is { } files)
            entries.Add((GnomeCopiedFiles, Encoding.UTF8.GetBytes(FileUriList.BuildGnomeCopiedFiles(files))));
        if (package.Url is { } url)
            entries.Add((NetscapeUrl, Encoding.UTF8.GetBytes(url.AbsoluteUri)));
        // A standard format already offered for this target wins over a colliding custom key.
        foreach (var (mimeType, data) in package.Custom)
        {
            if (!entries.Any(e => e.Target.Equals(mimeType, StringComparison.OrdinalIgnoreCase)))
                entries.Add((mimeType, data.ToArray()));
        }

        return entries;
    }

    // _NETSCAPE_URL is the link, then optionally a newline and its title.
    public static Uri? ParseNetscapeUrl(ReadOnlySpan<byte> data) =>
        FileUriList.FirstUrl(Encoding.UTF8.GetString(data).TrimEnd('\0').Split('\n')[0]);

    public static string DecodeHtml(ReadOnlySpan<byte> data)
    {
        string html;
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
            html = Encoding.Unicode.GetString(data[2..]);
        else if (data.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
            html = Encoding.BigEndianUnicode.GetString(data[2..]);
        else if (data.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
            html = Encoding.UTF8.GetString(data[3..]);
        else
            html = Encoding.UTF8.GetString(data);

        return html.TrimEnd('\0');
    }
}
