// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Web.Hosting;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class MimeTypesTests
{
    [Theory]
    [InlineData("index.html", "text/html")]
    [InlineData("index.htm", "text/html")]
    [InlineData("style.css", "text/css")]
    [InlineData("app.js", "application/javascript")]
    [InlineData("module.mjs", "application/javascript")]
    [InlineData("data.json", "application/json")]
    [InlineData("feed.xml", "application/xml")]
    [InlineData("logo.svg", "image/svg+xml")]
    [InlineData("photo.png", "image/png")]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("anim.gif", "image/gif")]
    [InlineData("photo.webp", "image/webp")]
    [InlineData("photo.avif", "image/avif")]
    [InlineData("favicon.ico", "image/x-icon")]
    [InlineData("font.woff", "font/woff")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("font.ttf", "font/ttf")]
    [InlineData("font.otf", "font/otf")]
    [InlineData("font.eot", "application/vnd.ms-fontobject")]
    [InlineData("app.wasm", "application/wasm")]
    [InlineData("app.js.map", "application/json")]
    [InlineData("readme.txt", "text/plain")]
    [InlineData("video.webm", "video/webm")]
    [InlineData("video.mp4", "video/mp4")]
    [InlineData("audio.mp3", "audio/mpeg")]
    [InlineData("audio.ogg", "audio/ogg")]
    [InlineData("audio.wav", "audio/wav")]
    public void GetContentType_KnownExtension_ReturnsCorrectMimeType(string path, string expected)
    {
        var result = MimeTypes.GetContentType(path);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetContentType_UnknownExtension_ReturnsOctetStream()
    {
        var result = MimeTypes.GetContentType("archive.xyz");

        Assert.Equal("application/octet-stream", result);
    }

    [Fact]
    public void GetContentType_PathWithDirectories_ReturnsCorrectType()
    {
        var result = MimeTypes.GetContentType("assets/css/style.css");

        Assert.Equal("text/css", result);
    }
}
