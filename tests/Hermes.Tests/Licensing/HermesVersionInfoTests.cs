// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Licensing;
using Xunit;

namespace Hermes.Tests.Licensing;

public sealed class HermesVersionInfoTests
{
    [Theory]
    [InlineData("2026-04-24", 2026, 4, 24)]
    [InlineData("2025-01-01", 2025, 1, 1)]
    [InlineData("2030-12-31", 2030, 12, 31)]
    public void ParseReleaseDate_valid_string_returns_correct_date(string input, int year, int month, int day)
    {
        var result = HermesVersionInfo.ParseReleaseDate(input);
        Assert.Equal(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void ParseReleaseDate_invalid_string_returns_UtcNow_fallback(string? input)
    {
        var before = DateTime.UtcNow.Date;
        var result = HermesVersionInfo.ParseReleaseDate(input);
        var after = DateTime.UtcNow.Date;

        Assert.InRange(result.Date, before, after);
    }
}
