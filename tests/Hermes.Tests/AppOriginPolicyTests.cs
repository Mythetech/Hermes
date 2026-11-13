// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Infrastructure;
using Xunit;

namespace Hermes.Tests;

public sealed class AppOriginPolicyTests
{
    [Theory]
    [InlineData("http", "http://localhost/*")]
    [InlineData("https", "https://localhost/*")]
    [InlineData("HTTPS", "HTTPS://localhost/*")]
    [InlineData("app", "app://*")]
    public void RequestFilter_ScopesWebSchemesToTheAppHost(string scheme, string expected)
    {
        Assert.Equal(expected, AppOriginPolicy.RequestFilter(scheme));
    }

    [Theory]
    [InlineData("http://localhost/", true)]
    [InlineData("http://localhost/frame.html", true)]
    [InlineData("http://LOCALHOST/_framework/x.js", true)]
    [InlineData("http://localhost:80/", true)]
    [InlineData("https://localhost:443/", true)]
    [InlineData("http://localhost:5000/", false)]
    [InlineData("http://127.0.0.1:5123/", false)]
    [InlineData("http://127.0.0.1/", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("app://anything/", true)]
    public void IsAppRequest(string url, bool expected)
    {
        Assert.Equal(expected, AppOriginPolicy.IsAppRequest(new Uri(url)));
    }

    [Fact]
    public void CorsOrigin_IsTheRequestsOwnOrigin()
    {
        Assert.Equal("http://localhost", AppOriginPolicy.CorsOrigin(new Uri("http://localhost/_content/a/b.js")));
    }

    [Theory]
    [InlineData("http://localhost/page", "http://localhost/", true)]
    [InlineData("http://LOCALHOST/page", "http://localhost/", true)]
    [InlineData("http://localhost:5173/x", "http://localhost:5173/", true)]
    [InlineData("about:blank", "about:blank", true)]
    [InlineData("http://127.0.0.1:5123/probe.html", "http://localhost/", false)]
    [InlineData("http://localhost:5000/", "http://localhost/", false)]
    [InlineData("https://localhost/", "http://localhost/", false)]
    [InlineData(null, "http://localhost/", false)]
    [InlineData("http://localhost/", null, false)]
    [InlineData("not a uri", "http://localhost/", false)]
    public void IsSameOrigin(string? sender, string? topLevel, bool expected)
    {
        Assert.Equal(expected, AppOriginPolicy.IsSameOrigin(sender, topLevel));
    }

    [Fact]
    public void IsSameOrigin_IdenticalUrlsTooLongForUri()
    {
        var page = "data:text/html," + new string('a', 70_000);

        Assert.True(AppOriginPolicy.IsSameOrigin(page, page));
        Assert.False(AppOriginPolicy.IsSameOrigin(page, "http://localhost/"));
    }

    [Fact]
    public void IsSameOrigin_BothNull_IsFalse()
    {
        Assert.False(AppOriginPolicy.IsSameOrigin(null, null));
    }
}
