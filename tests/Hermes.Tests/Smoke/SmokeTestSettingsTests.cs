// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Diagnostics;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeTestSettingsTests
{
    private static SmokeTestSettings Parse(params (string Name, string Value)[] variables)
    {
        var values = variables.ToDictionary(v => v.Name, v => v.Value);
        return SmokeTestSettings.FromEnvironment(name => values.TryGetValue(name, out var value) ? value : null);
    }

    [Fact]
    public void NothingSet_IsDisabledWithDefaults()
    {
        var settings = Parse();

        Assert.False(settings.IsEnabled);
        Assert.Equal(TimeSpan.FromSeconds(60), settings.Timeout);
        Assert.Null(settings.ResultPath);
        Assert.True(settings.ExitWhenDone);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData("", false)]
    public void IsEnabled_OnlyForOne(string value, bool expected)
    {
        Assert.Equal(expected, Parse(("HERMES_SMOKE_TEST", value)).IsEnabled);
    }

    [Theory]
    [InlineData("15", 15)]
    [InlineData("abc", 60)]
    [InlineData("0", 60)]
    [InlineData("-5", 60)]
    public void Timeout_FallsBackToSixtySeconds_WhenNotAPositiveInteger(string value, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Parse(("HERMES_SMOKE_TEST_TIMEOUT", value)).Timeout);
    }

    [Fact]
    public void ResultPath_IsNull_WhenBlank()
    {
        Assert.Null(Parse(("HERMES_SMOKE_TEST_RESULT", "  ")).ResultPath);
    }

    [Fact]
    public void ResultPath_IsKept_WhenSet()
    {
        Assert.Equal("/tmp/smoke/result.json", Parse(("HERMES_SMOKE_TEST_RESULT", "/tmp/smoke/result.json")).ResultPath);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    public void ExitWhenDone_IsFalseOnlyForZero(string value, bool expected)
    {
        Assert.Equal(expected, Parse(("HERMES_SMOKE_TEST_EXIT", value)).ExitWhenDone);
    }
}
