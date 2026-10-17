// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;

namespace Hermes.DataTransfer;

internal static class DataPackageValidator
{
    private static readonly byte[] s_pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly HashSet<string> s_standardTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/html",
        "image/png",
        "text/uri-list",
    };

    public static void ValidateForWrite(HermesDataPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.IsEmpty)
            throw new ArgumentException("The package has no data to write.", nameof(package));

        if (package.Png is { } png && !png.Span.StartsWith(s_pngSignature))
            throw new ArgumentException("Png does not contain PNG data.", nameof(package));

        if (package.Files is { } files)
        {
            if (files.Count == 0)
                throw new ArgumentException("Files is empty; leave it null to write no files.", nameof(package));

            foreach (var file in files)
            {
                if (string.IsNullOrEmpty(file) || !Path.IsPathFullyQualified(file))
                    throw new ArgumentException($"File path is not absolute: '{file}'.", nameof(package));

                if (file.Contains('\0'))
                    throw new ArgumentException("File path contains a NUL character.", nameof(package));
            }
        }

        if (package.Url is { } url && (!url.IsAbsoluteUri || url.IsFile))
            throw new ArgumentException($"Url must be an absolute, non-file URL: '{url.OriginalString}'.", nameof(package));

        foreach (var mimeType in package.Custom.Keys)
            ThrowIfInvalidCustomType(mimeType, nameof(package));
    }

    public static void ThrowIfInvalidCustomType(string mimeType, string paramName)
    {
        if (!IsMimeType(mimeType))
            throw new ArgumentException($"'{mimeType}' is not a type/subtype MIME type.", paramName);

        // A parameterized key such as text/plain;charset=utf-8 would collide with the platform's standard text target.
        if (mimeType.Contains(';'))
            throw new ArgumentException($"'{mimeType}' must not carry MIME parameters.", paramName);

        if (s_standardTypes.Contains(mimeType))
            throw new ArgumentException($"'{mimeType}' is a standard format; use the matching package property.", paramName);
    }

    private static bool IsMimeType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return false;

        var slash = mimeType.IndexOf('/');
        return slash > 0
            && slash < mimeType.Length - 1
            && mimeType.IndexOf('/', slash + 1) < 0
            && !mimeType.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
    }
}
