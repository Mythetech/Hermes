using Hermes.Testing;
using Hermes.Testing.Assertions;
using Xunit;

namespace Hermes.Tests.Testing;

public class TestableHermesWindowTests
{
    [Fact]
    public void SetTitle_ConfiguresWindow()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("My Test Window")
            .SetSize(800, 600);

        testWindow.Show();

        Assert.Equal("My Test Window", testWindow.Backend.Title);
        Assert.Equal((800, 600), testWindow.Backend.Size);
    }

    [Fact]
    public void SimulateMaximize_TriggersHandler()
    {
        using var testWindow = new TestableHermesWindow();
        var maximizedCalled = false;
        testWindow.OnMaximized(() => maximizedCalled = true);
        testWindow.Show();

        testWindow.SimulateMaximize();

        Assert.True(maximizedCalled);
        HermesAssert.WasMaximized(testWindow.Recording);
    }

    [Fact]
    public void SimulateWebMessage_TriggersHandler()
    {
        using var testWindow = new TestableHermesWindow();
        string? receivedMessage = null;
        testWindow.OnWebMessage(msg => receivedMessage = msg);
        testWindow.Show();

        testWindow.SimulateWebMessage("""{"type":"test","data":"hello"}""");

        Assert.Equal("""{"type":"test","data":"hello"}""", receivedMessage);
        HermesAssert.ReceivedWebMessageMatching(testWindow.Recording, "hello");
    }

    [Fact]
    public void SimulateDragRegionClick_RecordsAction()
    {
        using var testWindow = new TestableHermesWindow()
            .SetCustomTitleBar(true);
        testWindow.Show();

        testWindow.SimulateDragRegionClick();

        HermesAssert.DetectedDragRegionClick(testWindow.Recording);
    }

    [Fact]
    public void SimulateDragRegionDoubleClick_RecordsAction()
    {
        using var testWindow = new TestableHermesWindow()
            .SetCustomTitleBar(true);
        testWindow.Show();

        testWindow.SimulateDragRegionDoubleClick();

        HermesAssert.DetectedDragRegionDoubleClick(testWindow.Recording);
    }

    [Fact]
    public void Load_RecordsNavigation()
    {
        using var testWindow = new TestableHermesWindow()
            .Load("https://example.com");
        testWindow.Show();

        HermesAssert.NavigatedTo(testWindow.Recording, "https://example.com");
    }

    [Fact]
    public void Close_RaisesClosingEvent()
    {
        using var testWindow = new TestableHermesWindow();
        var closingCalled = false;
        testWindow.OnClosing(() => closingCalled = true);
        testWindow.Show();

        testWindow.Close();

        Assert.True(closingCalled);
        HermesAssert.WasClosed(testWindow.Backend);
    }

    [Fact]
    public void SimulateResize_TriggersHandler()
    {
        using var testWindow = new TestableHermesWindow();
        (int w, int h) resizedTo = (0, 0);
        testWindow.OnResized((w, h) => resizedTo = (w, h));
        testWindow.Show();

        testWindow.SimulateResize(1920, 1080);

        Assert.Equal((1920, 1080), resizedTo);
        HermesAssert.HasSize(testWindow.Backend, 1920, 1080);
    }
}
