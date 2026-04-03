// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Xunit;

namespace Hermes.Tests;

public sealed class OpenerTests
{
    [Fact]
    public void OpenUrl_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Opener.OpenUrl(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenUrl_Empty_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() => Opener.OpenUrl(url));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://docs.mythetech.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>hi</h1>")]
    public void OpenUrl_DisallowedScheme_ThrowsArgumentException(string url)
    {
        Assert.Throws<ArgumentException>(() => Opener.OpenUrl(url));
    }

    [Theory]
    [InlineData("http://docs.mythetech.com/smoke-test")]
    [InlineData("https://docs.mythetech.com/smoke-test")]
    [InlineData("HTTP://DOCS.MYTHETECH.COM/SMOKE-TEST")]
    [InlineData("HTTPS://DOCS.MYTHETECH.COM/SMOKE-TEST")]
    public void OpenUrl_ValidScheme_DoesNotThrowArgumentException(string url)
    {
        // These will attempt to launch a browser, so we just verify no ArgumentException.
        // Process.Start may throw on CI without a desktop, so we catch non-argument exceptions.
        try
        {
            Opener.OpenUrl(url);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            // Expected on headless environments
        }
    }

    [Fact]
    public void OpenFile_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Opener.OpenFile(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenFile_Empty_ThrowsArgumentException(string path)
    {
        Assert.Throws<ArgumentException>(() => Opener.OpenFile(path));
    }

    [Fact]
    public void OpenFile_NonExistentPath_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => Opener.OpenFile("/nonexistent/path/to/file.txt"));
    }

    [Fact]
    public void RevealInFileManager_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Opener.RevealInFileManager(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RevealInFileManager_Empty_ThrowsArgumentException(string path)
    {
        Assert.Throws<ArgumentException>(() => Opener.RevealInFileManager(path));
    }

    [Fact]
    public void RevealInFileManager_NonExistentPath_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => Opener.RevealInFileManager("/nonexistent/path/to/file.txt"));
    }
}
