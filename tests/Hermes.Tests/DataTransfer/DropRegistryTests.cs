// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class DropRegistryTests
{
    private static readonly HermesDataPackage s_package = new() { Text = "x" };

    private readonly FakeTimeProvider _time = new();
    private readonly object _owner = new();
    private readonly object _otherOwner = new();

    [Fact]
    public void Register_IssuesUniqueLowercaseHexIds()
    {
        var registry = new DropRegistry(_time);

        var ids = Enumerable.Range(0, 50).Select(_ => registry.Register(_owner, 0, 0, s_package).Id).ToList();

        Assert.All(ids, id => Assert.Matches("^[0-9a-f]{32}$", id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Register_KeepsThePointAndData()
    {
        var drop = new DropRegistry(_time).Register(_owner, 1.5, 2, s_package);

        Assert.Equal(1.5, drop.X);
        Assert.Equal(2, drop.Y);
        Assert.Same(s_package, drop.Data);
    }

    [Fact]
    public void TryGet_ScopesToTheOwner_UnlessOwnerIsNull()
    {
        var registry = new DropRegistry(_time);
        var drop = registry.Register(_owner, 0, 0, s_package);

        Assert.True(registry.TryGet(drop.Id, _owner, out var mine));
        Assert.Same(drop, mine);
        Assert.False(registry.TryGet(drop.Id, _otherOwner, out _));
        Assert.True(registry.TryGet(drop.Id, owner: null, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void TryGet_RejectsMissingAndUnknownIds(string? id) =>
        Assert.False(new DropRegistry(_time).TryGet(id, owner: null, out _));

    [Fact]
    public void TryGet_ExpiresAfterTheLifetime()
    {
        var registry = new DropRegistry(_time);
        var drop = registry.Register(_owner, 0, 0, s_package);

        _time.Advance(DropRegistry.Lifetime - TimeSpan.FromTicks(1));
        Assert.True(registry.TryGet(drop.Id, _owner, out _));

        _time.Advance(TimeSpan.FromTicks(1));
        Assert.False(registry.TryGet(drop.Id, _owner, out _));
    }

    [Fact]
    public void Register_EvictsTheOwnersOldestPastTheCap_AndLeavesOtherOwnersAlone()
    {
        var registry = new DropRegistry(_time);
        var other = registry.Register(_otherOwner, 0, 0, s_package);
        var drops = Enumerable.Range(0, DropRegistry.MaxDropsPerOwner + 1)
            .Select(_ => registry.Register(_owner, 0, 0, s_package))
            .ToList();

        Assert.False(registry.TryGet(drops[0].Id, _owner, out _));
        Assert.True(registry.TryGet(drops[1].Id, _owner, out _));
        Assert.True(registry.TryGet(drops[^1].Id, _owner, out _));
        Assert.True(registry.TryGet(other.Id, _otherOwner, out _));
    }

    [Fact]
    public void Release_RemovesOnlyThatOwnersDrops()
    {
        var registry = new DropRegistry(_time);
        var mine = registry.Register(_owner, 0, 0, s_package);
        var theirs = registry.Register(_otherOwner, 0, 0, s_package);

        registry.Release(_owner);

        Assert.False(registry.TryGet(mine.Id, owner: null, out _));
        Assert.True(registry.TryGet(theirs.Id, owner: null, out _));
    }
}
