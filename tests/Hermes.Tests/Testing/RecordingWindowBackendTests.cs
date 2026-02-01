using Hermes.Abstractions;
using Hermes.Testing;
using Hermes.Testing.Assertions;
using Xunit;

namespace Hermes.Tests.Testing;

public class RecordingWindowBackendTests
{
    [Fact]
    public void Initialize_RecordsOptionsAndSetsState()
    {
        var backend = new RecordingWindowBackend();
        var options = new HermesWindowOptions
        {
            Title = "Test Window",
            Width = 1024,
            Height = 768,
            X = 100,
            Y = 200
        };

        backend.Initialize(options);

        Assert.True(backend.IsInitialized);
        Assert.Equal("Test Window", backend.Title);
        Assert.Equal((1024, 768), backend.Size);
        Assert.Equal((100, 200), backend.Position);
        Assert.Same(options, backend.InitialOptions);
        Assert.True(backend.Recording.MethodWasCalled("Initialize"));
    }

    [Fact]
    public void Show_RecordsMethodCallAndSetsState()
    {
        var backend = new RecordingWindowBackend();

        backend.Show();

        Assert.True(backend.IsShown);
        Assert.True(backend.Recording.MethodWasCalled("Show"));
    }

    [Fact]
    public void Close_RecordsMethodCallAndRaisesClosingEvent()
    {
        var backend = new RecordingWindowBackend();
        var closingCalled = false;
        backend.Closing += () => closingCalled = true;

        backend.Close();

        Assert.True(backend.IsClosed);
        Assert.True(closingCalled);
        Assert.True(backend.Recording.EventWasRaised("Closing"));
    }

    [Fact]
    public void NavigateToUrl_RecordsNavigation()
    {
        var backend = new RecordingWindowBackend();

        backend.NavigateToUrl("https://example.com");

        Assert.True(backend.Recording.NavigatedTo("https://example.com"));
        Assert.True(backend.Recording.MethodWasCalled("NavigateToUrl"));
    }

    [Fact]
    public void SendWebMessage_RecordsMessage()
    {
        var backend = new RecordingWindowBackend();

        backend.SendWebMessage("test message");

        Assert.Contains("test message", backend.Recording.WebMessagesSent);
        Assert.True(backend.Recording.MethodWasCalled("SendWebMessage"));
    }

    [Fact]
    public void SimulateWebMessage_RecordsAndRaisesEvent()
    {
        var backend = new RecordingWindowBackend();
        string? receivedMessage = null;
        backend.WebMessageReceived += msg => receivedMessage = msg;

        backend.SimulateWebMessage("hello from JS");

        Assert.Equal("hello from JS", receivedMessage);
        Assert.Contains("hello from JS", backend.Recording.WebMessagesReceived);
    }

    [Fact]
    public void SimulateResize_UpdatesSizeAndRaisesEvent()
    {
        var backend = new RecordingWindowBackend();
        (int w, int h) resizedTo = (0, 0);
        backend.Resized += (w, h) => resizedTo = (w, h);

        backend.SimulateResize(1920, 1080);

        Assert.Equal((1920, 1080), backend.Size);
        Assert.Equal((1920, 1080), resizedTo);
        Assert.True(backend.Recording.EventWasRaised("Resized"));
    }

    [Fact]
    public void SimulateMaximize_SetsStateAndRaisesEvent()
    {
        var backend = new RecordingWindowBackend();
        var maximizedCalled = false;
        backend.Maximized += () => maximizedCalled = true;

        backend.SimulateMaximize();

        Assert.True(backend.IsMaximized);
        Assert.True(maximizedCalled);
        Assert.True(backend.Recording.EventWasRaised("Maximized"));
    }

    [Fact]
    public void SimulateRestore_SetsStateAndRaisesEvent()
    {
        var backend = new RecordingWindowBackend();
        backend.SimulateMaximize(); // First maximize
        var restoredCalled = false;
        backend.Restored += () => restoredCalled = true;

        backend.SimulateRestore();

        Assert.False(backend.IsMaximized);
        Assert.True(restoredCalled);
        Assert.True(backend.Recording.EventWasRaised("Restored"));
    }

    [Fact]
    public void SimulateDragRegionClick_RecordsDragAction()
    {
        var backend = new RecordingWindowBackend();

        backend.SimulateDragRegionClick();

        Assert.Equal("drag", backend.Recording.LastDragAction);
    }

    [Fact]
    public void SimulateDragRegionDoubleClick_RecordsDragAction()
    {
        var backend = new RecordingWindowBackend();

        backend.SimulateDragRegionDoubleClick();

        Assert.Equal("double-click", backend.Recording.LastDragAction);
    }

    [Fact]
    public void SimulateNonDragRegionClick_RecordsDragAction()
    {
        var backend = new RecordingWindowBackend();

        backend.SimulateNonDragRegionClick();

        Assert.Equal("no-drag", backend.Recording.LastDragAction);
    }

    [Fact]
    public void RegisterCustomScheme_RecordsAndAllowsTesting()
    {
        var backend = new RecordingWindowBackend();

        backend.RegisterCustomScheme("app", url =>
        {
            if (url.Contains("test.html"))
                return (new MemoryStream("<html>test</html>"u8.ToArray()), "text/html");
            return (null, null);
        });

        Assert.True(backend.HasCustomScheme("app"));
        var result = backend.TestCustomScheme("app", "app://test.html");
        Assert.NotNull(result);
        Assert.NotNull(result.Value.Content);
        Assert.Equal("text/html", result.Value.ContentType);
    }

    [Fact]
    public void Recording_Clear_RemovesAllRecordedData()
    {
        var backend = new RecordingWindowBackend();
        backend.NavigateToUrl("https://example.com");
        backend.SendWebMessage("test");
        backend.SimulateWebMessage("response");
        backend.SimulateMaximize();

        backend.Recording.Clear();

        Assert.Empty(backend.Recording.Navigations);
        Assert.Empty(backend.Recording.WebMessagesSent);
        Assert.Empty(backend.Recording.WebMessagesReceived);
        Assert.Empty(backend.Recording.Events);
        Assert.Empty(backend.Recording.MethodCalls);
        Assert.Null(backend.Recording.LastDragAction);
    }
}
