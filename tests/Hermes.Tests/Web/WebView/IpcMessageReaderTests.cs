// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json;
using Hermes.Blazor.WebView;
using Xunit;

namespace Hermes.Tests.Web.WebView;

public class IpcMessageReaderTests
{
    [Fact]
    public void AttachPage_IsParsedWithItsArguments()
    {
        var found = IpcMessageReader.TryRead("""__bwv:["AttachPage","app://localhost/","app://localhost/counter"]""", out var type, out var args);

        Assert.True(found);
        Assert.Equal(IncomingMessageType.AttachPage, type);
        Assert.Equal(2, args.Count);
        Assert.Equal("app://localhost/", args[0].GetString());
        Assert.Equal("app://localhost/counter", args[1].GetString());
    }

    [Fact]
    public void EveryIncomingType_IsRecognized()
    {
        foreach (var name in Enum.GetNames<IncomingMessageType>())
        {
            Assert.True(IpcMessageReader.TryRead($"__bwv:[\"{name}\"]", out var type, out var args));
            Assert.Equal(Enum.Parse<IncomingMessageType>(name), type);
            Assert.Equal(0, args.Count);
        }
    }

    [Fact]
    public void ArgumentsStayReadableAfterTheCall()
    {
        IpcMessageReader.TryRead("""__bwv:["ReceiveByteArrayFromJS",0,"AP8Q"]""", out _, out var args);

        GC.Collect();

        Assert.Equal(0, args[0].GetInt32());
        Assert.Equal(new byte[] { 0, 255, 16 }, args[1].GetBytesFromBase64());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("hello from the app's own channel")]
    [InlineData("__bwv[\"AttachPage\"]")]
    public void MessagesWithoutThePrefix_AreIgnored(string? message)
    {
        Assert.False(IpcMessageReader.TryRead(message, out _, out _));
    }

    [Theory]
    [InlineData("__bwv:[")]
    [InlineData("__bwv:{}")]
    [InlineData("__bwv:[]")]
    [InlineData("__bwv:[42]")]
    public void MalformedPrefixedMessages_Throw(string message)
    {
        Assert.ThrowsAny<JsonException>(() => IpcMessageReader.TryRead(message, out _, out _));
    }

    [Fact]
    public void UnknownMessageType_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => IpcMessageReader.TryRead("""__bwv:["Bogus",1]""", out _, out _));
        Assert.Contains("Bogus", ex.Message);
    }
}
