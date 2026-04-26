// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Reflection;

namespace Hermes.Licensing;

internal static class HermesVersionInfo
{
    internal static DateTime ReleaseDate { get; } = GetReleaseDate();

    private static DateTime GetReleaseDate()
    {
        var attr = typeof(HermesVersionInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "HermesReleaseDate");

        return ParseReleaseDate(attr?.Value);
    }

    internal static DateTime ParseReleaseDate(string? value)
    {
        if (value is not null && DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var date))
            return date.ToUniversalTime();

        return DateTime.UtcNow;
    }
}
