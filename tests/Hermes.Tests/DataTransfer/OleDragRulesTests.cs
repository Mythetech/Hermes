// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class OleDragRulesTests
{
    [Fact]
    public void ToResult_ACopyATargetTook_IsDropped() =>
        Assert.Equal(HermesDragResult.Dropped, OleDragRules.ToResult(OleDragRules.DragDropSDrop, OleDragRules.DropEffectCopy));

    [Fact]
    public void ToResult_AReleaseOverNoTarget_IsCanceled() =>
        Assert.Equal(HermesDragResult.Canceled, OleDragRules.ToResult(OleDragRules.DragDropSDrop, OleDragRules.DropEffectNone));

    [Theory]
    [InlineData(OleDragRules.DropEffectNone)]
    [InlineData(OleDragRules.DropEffectCopy)]
    public void ToResult_ACanceledDrag_IsCanceled(uint effect) =>
        Assert.Equal(HermesDragResult.Canceled, OleDragRules.ToResult(OleDragRules.DragDropSCancel, effect));

    [Fact]
    public void ToResult_AFailedDrag_Throws()
    {
        const int failure = unchecked((int)0x80004005);

        var ex = Assert.Throws<COMException>(() => OleDragRules.ToResult(failure, OleDragRules.DropEffectNone));

        Assert.Equal(failure, ex.HResult);
    }

    [Theory]
    [InlineData(false, 0x01)]
    [InlineData(true, 0x02)]
    public void PrimaryButtonKey_FollowsTheButtonSwap(bool swapped, int expected) =>
        Assert.Equal(expected, OleDragRules.PrimaryButtonKey(swapped));

    [Theory]
    [InlineData(unchecked((short)0x8000), true)]
    [InlineData(unchecked((short)0x8001), true)]
    [InlineData((short)0x0001, false)]
    [InlineData((short)0, false)]
    public void IsDown_ReadsTheHighBit(short state, bool expected) =>
        Assert.Equal(expected, OleDragRules.IsDown(state));
}
