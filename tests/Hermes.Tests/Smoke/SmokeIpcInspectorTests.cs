// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using Hermes.Blazor.Diagnostics;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeIpcInspectorTests
{
    // Mirrors IpcCommon.Serialize in Microsoft.AspNetCore.Components.WebView: a prefix, then a JSON array
    // whose first element is the message type.
    private static string BlazorMessage(params object?[] typeAndArgs) => "__bwv:" + JsonSerializer.Serialize(typeAndArgs);

    [Fact]
    public void ReadsTheMessageAndStackTrace()
    {
        var found = SmokeIpcInspector.TryReadUnhandledException(
            BlazorMessage("NotifyUnhandledException", "Theme palette was \"null\"", "   at App.Render()"),
            out var error,
            out var stackTrace);

        Assert.True(found);
        Assert.Equal("Theme palette was \"null\"", error);
        Assert.Equal("   at App.Render()", stackTrace);
    }

    [Fact]
    public void ToleratesAMissingStackTrace()
    {
        var found = SmokeIpcInspector.TryReadUnhandledException(BlazorMessage("NotifyUnhandledException", "boom"), out var error, out var stackTrace);

        Assert.True(found);
        Assert.Equal("boom", error);
        Assert.Null(stackTrace);
    }

    [Theory]
    [InlineData("hello from the page")]
    [InlineData("__bwv:[\"RenderBatch\",1,\"AAAA\"]")]
    [InlineData("__bwv:[\"NotifyUnhandledException\",\"unterminated")]
    public void IgnoresEverythingElse(string message)
    {
        Assert.False(SmokeIpcInspector.TryReadUnhandledException(message, out _, out _));
    }
}
