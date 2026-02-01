using Hermes.Abstractions;
using Hermes.Testing;
using Hermes.Testing.Assertions;
using Xunit;

namespace Hermes.Tests.Testing;

public class HermesAssertTests
{
    [Fact]
    public void WasInitialized_PassesWhenInitialized()
    {
        var backend = new RecordingWindowBackend();
        backend.Initialize(new HermesWindowOptions());

        HermesAssert.WasInitialized(backend);
    }

    [Fact]
    public void WasInitialized_FailsWhenNotInitialized()
    {
        var backend = new RecordingWindowBackend();

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.WasInitialized(backend));
    }

    [Fact]
    public void WasMaximized_PassesWhenMaximizedEventRaised()
    {
        var backend = new RecordingWindowBackend();
        backend.SimulateMaximize();

        HermesAssert.WasMaximized(backend.Recording);
    }

    [Fact]
    public void WasMaximized_FailsWhenNeverMaximized()
    {
        var backend = new RecordingWindowBackend();

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.WasMaximized(backend.Recording));
    }

    [Fact]
    public void IsMaximized_PassesWhenCurrentlyMaximized()
    {
        var backend = new RecordingWindowBackend();
        backend.SimulateMaximize();

        HermesAssert.IsMaximized(backend);
    }

    [Fact]
    public void IsMaximized_FailsWhenNotMaximized()
    {
        var backend = new RecordingWindowBackend();

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.IsMaximized(backend));
    }

    [Fact]
    public void HasSize_PassesWhenSizeMatches()
    {
        var backend = new RecordingWindowBackend();
        backend.Size = (1024, 768);

        HermesAssert.HasSize(backend, 1024, 768);
    }

    [Fact]
    public void HasSize_FailsWhenSizeDiffers()
    {
        var backend = new RecordingWindowBackend();
        backend.Size = (800, 600);

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.HasSize(backend, 1024, 768));
    }

    [Fact]
    public void HasTitle_PassesWhenTitleMatches()
    {
        var backend = new RecordingWindowBackend();
        backend.Title = "Test Window";

        HermesAssert.HasTitle(backend, "Test Window");
    }

    [Fact]
    public void HasTitle_FailsWhenTitleDiffers()
    {
        var backend = new RecordingWindowBackend();
        backend.Title = "Wrong Title";

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.HasTitle(backend, "Test Window"));
    }

    [Fact]
    public void NavigatedTo_PassesWhenUrlMatches()
    {
        var backend = new RecordingWindowBackend();
        backend.NavigateToUrl("https://example.com");

        HermesAssert.NavigatedTo(backend.Recording, "https://example.com");
    }

    [Fact]
    public void NavigatedTo_FailsWhenUrlNotVisited()
    {
        var backend = new RecordingWindowBackend();
        backend.NavigateToUrl("https://other.com");

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.NavigatedTo(backend.Recording, "https://example.com"));
    }

    [Fact]
    public void NavigatedToPattern_PassesWhenPatternMatches()
    {
        var backend = new RecordingWindowBackend();
        backend.NavigateToUrl("https://example.com/page/123");

        HermesAssert.NavigatedToPattern(backend.Recording, "example.com");
    }

    [Fact]
    public void SentWebMessage_PassesWhenMessageSent()
    {
        var backend = new RecordingWindowBackend();
        backend.SendWebMessage("test message");

        HermesAssert.SentWebMessage(backend.Recording, "test message");
    }

    [Fact]
    public void ReceivedWebMessage_PassesWhenMessageReceived()
    {
        var backend = new RecordingWindowBackend();
        backend.SimulateWebMessage("response message");

        HermesAssert.ReceivedWebMessage(backend.Recording, "response message");
    }

    [Fact]
    public void DetectedDragAction_PassesWhenActionMatches()
    {
        var backend = new RecordingWindowBackend();
        backend.SimulateDragRegionClick();

        HermesAssert.DetectedDragAction(backend.Recording, "drag");
    }

    [Fact]
    public void DetectedDragAction_FailsWhenActionDiffers()
    {
        var backend = new RecordingWindowBackend();
        backend.SimulateNonDragRegionClick();

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.DetectedDragAction(backend.Recording, "drag"));
    }

    [Fact]
    public void MethodWasCalled_PassesWhenMethodCalled()
    {
        var backend = new RecordingWindowBackend();
        backend.Show();

        HermesAssert.MethodWasCalled(backend.Recording, "Show");
    }

    [Fact]
    public void MethodWasCalled_FailsWhenMethodNotCalled()
    {
        var backend = new RecordingWindowBackend();

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.MethodWasCalled(backend.Recording, "Show"));
    }

    [Fact]
    public void EventWasRaised_PassesWhenEventRaised()
    {
        var backend = new RecordingWindowBackend();
        backend.Close();

        HermesAssert.EventWasRaised(backend.Recording, "Closing");
    }

    [Fact]
    public void EventWasNotRaised_PassesWhenEventNotRaised()
    {
        var backend = new RecordingWindowBackend();

        HermesAssert.EventWasNotRaised(backend.Recording, "Maximized");
    }

    [Fact]
    public void CustomSchemeRegistered_PassesWhenRegistered()
    {
        var backend = new RecordingWindowBackend();
        backend.RegisterCustomScheme("app", _ => (null, null));

        HermesAssert.CustomSchemeRegistered(backend, "app");
    }

    [Fact]
    public void CustomSchemeRegistered_FailsWhenNotRegistered()
    {
        var backend = new RecordingWindowBackend();

        Assert.Throws<HermesAssertionException>(() =>
            HermesAssert.CustomSchemeRegistered(backend, "app"));
    }
}
