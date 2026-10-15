// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class DragDropScriptTests
{
    [Theory]
    [InlineData("type: 'hermes-drop'")]
    [InlineData("type: 'hermes-drop-reset'")]
    [InlineData("'hermes-drop-ready'")]
    [InlineData("'hermes-drop-simulate'")]
    [InlineData("'{\"type\":\"hermes-drop-'")]
    [InlineData("new CustomEvent('hermesdrop'")]
    [InlineData("registerCustomEventType('hermesdrop'")]
    [InlineData("postMessageWithAdditionalObjects")]
    [InlineData("event.isTrusted")]
    [InlineData("event.defaultPrevented")]
    [InlineData("window !== window.top")]
    public void Source_SpeaksTheDropProtocol(string fragment) =>
        Assert.Contains(fragment, DragDropScript.Source);

    [Fact]
    public void Source_NeverSendsPaths() =>
        Assert.DoesNotContain(".path", DragDropScript.Source);
}
