// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.DataTransfer;

/// <summary>
/// <c>text/uri-list</c> and GNOME's <c>x-special/gnome-copied-files</c>, used by the Linux clipboard.
/// </summary>
internal static class FileUriList
{
    public static string BuildUriList(IReadOnlyList<string> paths) =>
        string.Concat(paths.Select(p => ToFileUri(p) + "\r\n"));

    public static IReadOnlyList<string> ParseUriList(string text) =>
        text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(FromFileUri)
            .OfType<string>()
            .ToList();

    public static string BuildGnomeCopiedFiles(IReadOnlyList<string> paths) =>
        "copy\n" + string.Join('\n', paths.Select(ToFileUri));

    public static IReadOnlyList<string> ParseGnomeCopiedFiles(string text)
    {
        var lines = text.Split('\n');
        var skip = lines.Length > 0 && (lines[0].Trim() is "copy" or "cut") ? 1 : 0;
        return ParseUriList(string.Join('\n', lines.Skip(skip)));
    }

    // Escapes each segment by hand; new Uri(path) would read '#' as the start of a fragment.
    public static string ToFileUri(string path)
    {
        var normalized = Path.DirectorySeparatorChar == '\\' ? path.Replace('\\', '/') : path;
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        var segments = normalized.Split('/').Select(s => IsDriveSegment(s) ? s : Uri.EscapeDataString(s));
        return "file://" + string.Join('/', segments);
    }

    public static string? FromFileUri(string uri)
    {
        if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return null;

        var rest = uri["file://".Length..];
        var slash = rest.IndexOf('/');
        if (slash < 0)
            return null;

        var host = rest[..slash];
        if (host.Length > 0 && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = Uri.UnescapeDataString(rest[slash..]);
        if (OperatingSystem.IsWindows() && path.Length >= 3 && path[0] == '/' && IsDriveSegment(path[1..3]))
            path = path[1..].Replace('/', '\\');

        return path;
    }

    private static bool IsDriveSegment(string segment) =>
        segment.Length == 2 && char.IsAsciiLetter(segment[0]) && segment[1] == ':';
}
