// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Infrastructure;
using Xunit;

namespace Hermes.Tests;

public sealed class BridgeTokenTests
{
    [Fact]
    public void Create_Is32LowercaseHexChars()
    {
        var token = BridgeToken.Create();

        Assert.Equal(BridgeToken.Length, token.Length);
        Assert.Matches("^[0-9a-f]{32}$", token);
    }

    [Fact]
    public void Create_DiffersEveryCall()
    {
        Assert.NotEqual(BridgeToken.Create(), BridgeToken.Create());
    }
}
