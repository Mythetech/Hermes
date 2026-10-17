// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;
using Hermes.Hotkeys;
using Xunit;

namespace Hermes.Tests.Hotkeys;

public sealed class NativeHotkeyTableTests
{
    [ThreadStatic]
    private static bool t_completing;

    private readonly NativeHotkeyTable _table = new();

    [Fact]
    public async Task Registered_WithoutABoundTrigger_ReportsTheRequestedAccelerator()
    {
        var number = _table.Begin("open", "Ctrl+Alt+Shift+F12", out var completion);

        _table.Complete(number, 0, null, null);

        Assert.Equal(new HermesHotkeyResult(HermesHotkeyStatus.Registered, "Ctrl+Alt+Shift+F12", null), await completion);
        Assert.Equal("open", _table.IdFor(number));
    }

    [Fact]
    public async Task Registered_WithABoundTrigger_ReportsTheDesktopsTrigger()
    {
        var number = _table.Begin("open", "Ctrl+Alt+Shift+F12", out var completion);

        _table.Complete(number, 0, "Press <Control><Alt>K", null);

        Assert.Equal("Press <Control><Alt>K", (await completion).BoundAccelerator);
    }

    [Theory]
    [InlineData(1, HermesHotkeyStatus.Conflict)]
    [InlineData(2, HermesHotkeyStatus.Denied)]
    [InlineData(3, HermesHotkeyStatus.Unsupported)]
    [InlineData(4, HermesHotkeyStatus.Failed)]
    [InlineData(99, HermesHotkeyStatus.Failed)]
    public async Task ARefusal_MapsTheStatus_AndStopsMappingTheNumber(int nativeStatus, HermesHotkeyStatus expected)
    {
        var number = _table.Begin("open", "Ctrl+K", out var completion);

        _table.Complete(number, nativeStatus, null, "why");

        Assert.Equal(new HermesHotkeyResult(expected, null, "why"), await completion);
        Assert.Null(_table.IdFor(number));
        Assert.Null(_table.Remove("open"));
    }

    [Fact]
    public async Task ARefusalWithoutAReason_GetsOne()
    {
        var number = _table.Begin("open", "Ctrl+K", out var completion);

        _table.Complete(number, 1, null, null);

        Assert.False(string.IsNullOrEmpty((await completion).Reason));
    }

    [Fact]
    public void Remove_ReturnsTheNumberOnce_AndStopsPresses()
    {
        var number = _table.Begin("open", "Ctrl+K", out _);
        _table.Complete(number, 0, null, null);

        Assert.Equal(number, _table.Remove("open"));
        Assert.Null(_table.Remove("open"));
        Assert.Null(_table.IdFor(number));
    }

    [Fact]
    public async Task ARegistrationAnsweredAfterItsUnregister_StillCompletes()
    {
        var number = _table.Begin("open", "Ctrl+K", out var completion);
        _table.Remove("open");

        _table.Complete(number, 0, null, null);

        Assert.Equal(HermesHotkeyStatus.Registered, (await completion).Status);
        Assert.Null(_table.IdFor(number));
    }

    [Fact]
    public void ReRegisteringAnId_UsesANewNumber_AndALateRefusalOfTheOldOneKeepsTheNew()
    {
        var first = _table.Begin("open", "Ctrl+K", out _);
        _table.Remove("open");
        var second = _table.Begin("open", "Ctrl+K", out _);

        _table.Complete(first, 1, null, "late");
        _table.Complete(second, 0, null, null);

        Assert.NotEqual(first, second);
        Assert.Null(_table.IdFor(first));
        Assert.Equal("open", _table.IdFor(second));
        Assert.Equal(second, _table.Remove("open"));
    }

    [Fact]
    public void CompletingAnUnknownNumber_DoesNothing() =>
        _table.Complete(12345, 0, null, null);

    [Fact]
    public async Task Complete_NeverRunsTheCallersContinuationInline()
    {
        // With no context, an await continuation runs inline unless the source forbids it.
        SynchronizationContext.SetSynchronizationContext(null);
        var number = _table.Begin("open", "Ctrl+K", out var completion);
        var ranInline = ObserveAsync(completion);

        t_completing = true;
        try
        {
            _table.Complete(number, 0, null, null);
        }
        finally
        {
            t_completing = false;
        }

        Assert.False(await ranInline.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static async Task<bool> ObserveAsync(Task<HermesHotkeyResult> completion)
    {
        await completion;
        return t_completing;
    }
}
