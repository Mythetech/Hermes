// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Notifications;
using Xunit;

namespace Hermes.Tests.Notifications;

public sealed class ToastTagTests
{
    [Fact]
    public void FromId_ShortId_IsReturnedUnchanged()
    {
        var id = new string('a', 64);

        Assert.Equal(id, ToastTag.FromId(id));
    }

    [Fact]
    public void FromId_LongId_IsHashedTo64HexCharacters()
    {
        var id = new string('a', 65);

        var tag = ToastTag.FromId(id);

        Assert.Equal(64, tag.Length);
        Assert.Matches("^[0-9A-F]{64}$", tag);
        Assert.NotEqual(id, tag);
    }

    [Fact]
    public void FromId_IsStable()
    {
        var id = new string('b', 200);

        Assert.Equal(ToastTag.FromId(id), ToastTag.FromId(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void FromId_Blank_Throws(string id)
    {
        Assert.Throws<ArgumentException>(() => ToastTag.FromId(id));
    }
}
