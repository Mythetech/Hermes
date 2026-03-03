// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Testing.Assertions;

/// <summary>
/// Fluent assertions for testing Hermes window behavior.
/// These assertions throw <see cref="HermesAssertionException"/> on failure.
/// </summary>
public static class HermesAssert
{
    #region Window Lifecycle

    /// <summary>
    /// Assert that the window was initialized.
    /// </summary>
    public static void WasInitialized(RecordingWindowBackend backend)
    {
        if (!backend.IsInitialized)
            throw new HermesAssertionException("Expected window to be initialized, but it was not.");
    }

    /// <summary>
    /// Assert that the window was shown.
    /// </summary>
    public static void WasShown(RecordingWindowBackend backend)
    {
        if (!backend.IsShown)
            throw new HermesAssertionException("Expected window to be shown, but it was not.");
    }

    /// <summary>
    /// Assert that the window was closed.
    /// </summary>
    public static void WasClosed(RecordingWindowBackend backend)
    {
        if (!backend.IsClosed)
            throw new HermesAssertionException("Expected window to be closed, but it was not.");
    }

    #endregion

    #region Window State

    /// <summary>
    /// Assert that the window was maximized at some point.
    /// </summary>
    public static void WasMaximized(WindowBackendRecording recording)
    {
        if (!recording.EventWasRaised("Maximized"))
            throw new HermesAssertionException("Expected window to have been maximized, but Maximized event was never raised.");
    }

    /// <summary>
    /// Assert that the window was restored at some point.
    /// </summary>
    public static void WasRestored(WindowBackendRecording recording)
    {
        if (!recording.EventWasRaised("Restored"))
            throw new HermesAssertionException("Expected window to have been restored, but Restored event was never raised.");
    }

    /// <summary>
    /// Assert that the window is currently maximized.
    /// </summary>
    public static void IsMaximized(RecordingWindowBackend backend)
    {
        if (!backend.IsMaximized)
            throw new HermesAssertionException("Expected window to be maximized, but it is not.");
    }

    /// <summary>
    /// Assert that the window is not maximized.
    /// </summary>
    public static void IsNotMaximized(RecordingWindowBackend backend)
    {
        if (backend.IsMaximized)
            throw new HermesAssertionException("Expected window to not be maximized, but it is.");
    }

    /// <summary>
    /// Assert that the window has a specific size.
    /// </summary>
    public static void HasSize(RecordingWindowBackend backend, int expectedWidth, int expectedHeight)
    {
        var (width, height) = backend.Size;
        if (width != expectedWidth || height != expectedHeight)
            throw new HermesAssertionException($"Expected window size ({expectedWidth}, {expectedHeight}), but was ({width}, {height}).");
    }

    /// <summary>
    /// Assert that the window has a specific position.
    /// </summary>
    public static void HasPosition(RecordingWindowBackend backend, int expectedX, int expectedY)
    {
        var (x, y) = backend.Position;
        if (x != expectedX || y != expectedY)
            throw new HermesAssertionException($"Expected window position ({expectedX}, {expectedY}), but was ({x}, {y}).");
    }

    /// <summary>
    /// Assert that the window has a specific title.
    /// </summary>
    public static void HasTitle(RecordingWindowBackend backend, string expectedTitle)
    {
        if (backend.Title != expectedTitle)
            throw new HermesAssertionException($"Expected window title '{expectedTitle}', but was '{backend.Title}'.");
    }

    #endregion

    #region Navigation

    /// <summary>
    /// Assert that the window navigated to a specific URL.
    /// </summary>
    public static void NavigatedTo(WindowBackendRecording recording, string expectedUrl)
    {
        if (!recording.NavigatedTo(expectedUrl))
            throw new HermesAssertionException($"Expected navigation to '{expectedUrl}', but it never occurred. Navigations: [{string.Join(", ", recording.Navigations)}]");
    }

    /// <summary>
    /// Assert that the window navigated to a URL containing the given pattern.
    /// </summary>
    public static void NavigatedToPattern(WindowBackendRecording recording, string pattern)
    {
        if (!recording.NavigatedToPattern(pattern))
            throw new HermesAssertionException($"Expected navigation to URL containing '{pattern}', but none matched. Navigations: [{string.Join(", ", recording.Navigations)}]");
    }

    #endregion

    #region Web Messages

    /// <summary>
    /// Assert that a web message was sent to JavaScript.
    /// </summary>
    public static void SentWebMessage(WindowBackendRecording recording, string expectedMessage)
    {
        if (!recording.WebMessagesSent.Contains(expectedMessage))
            throw new HermesAssertionException($"Expected web message '{expectedMessage}' to be sent, but it was not. Sent messages: [{string.Join(", ", recording.WebMessagesSent)}]");
    }

    /// <summary>
    /// Assert that a web message matching a pattern was sent to JavaScript.
    /// </summary>
    public static void SentWebMessageMatching(WindowBackendRecording recording, string pattern)
    {
        if (!recording.SentWebMessageMatching(pattern))
            throw new HermesAssertionException($"Expected web message containing '{pattern}' to be sent, but none matched. Sent messages: [{string.Join(", ", recording.WebMessagesSent)}]");
    }

    /// <summary>
    /// Assert that a web message was received from JavaScript.
    /// </summary>
    public static void ReceivedWebMessage(WindowBackendRecording recording, string expectedMessage)
    {
        if (!recording.WebMessagesReceived.Contains(expectedMessage))
            throw new HermesAssertionException($"Expected web message '{expectedMessage}' to be received, but it was not. Received messages: [{string.Join(", ", recording.WebMessagesReceived)}]");
    }

    /// <summary>
    /// Assert that a web message matching a pattern was received from JavaScript.
    /// </summary>
    public static void ReceivedWebMessageMatching(WindowBackendRecording recording, string pattern)
    {
        if (!recording.ReceivedWebMessageMatching(pattern))
            throw new HermesAssertionException($"Expected web message containing '{pattern}' to be received, but none matched. Received messages: [{string.Join(", ", recording.WebMessagesReceived)}]");
    }

    #endregion

    #region Drag Regions (Custom Titlebar)

    /// <summary>
    /// Assert that a drag action was detected.
    /// </summary>
    public static void DetectedDragAction(WindowBackendRecording recording, string expectedAction)
    {
        if (recording.LastDragAction != expectedAction)
            throw new HermesAssertionException($"Expected drag action '{expectedAction}', but was '{recording.LastDragAction ?? "(none)"}'.");
    }

    /// <summary>
    /// Assert that a drag region click was detected.
    /// </summary>
    public static void DetectedDragRegionClick(WindowBackendRecording recording)
    {
        DetectedDragAction(recording, "drag");
    }

    /// <summary>
    /// Assert that a double-click on a drag region was detected.
    /// </summary>
    public static void DetectedDragRegionDoubleClick(WindowBackendRecording recording)
    {
        DetectedDragAction(recording, "double-click");
    }

    /// <summary>
    /// Assert that a non-drag region click was detected.
    /// </summary>
    public static void DetectedNonDragRegionClick(WindowBackendRecording recording)
    {
        DetectedDragAction(recording, "no-drag");
    }

    #endregion

    #region Method Calls

    /// <summary>
    /// Assert that a specific method was called.
    /// </summary>
    public static void MethodWasCalled(WindowBackendRecording recording, string methodName)
    {
        if (!recording.MethodWasCalled(methodName))
            throw new HermesAssertionException($"Expected method '{methodName}' to be called, but it was not. Called methods: [{string.Join(", ", recording.MethodCalls.Select(c => c.MethodName))}]");
    }

    /// <summary>
    /// Assert that a specific method was not called.
    /// </summary>
    public static void MethodWasNotCalled(WindowBackendRecording recording, string methodName)
    {
        if (recording.MethodWasCalled(methodName))
            throw new HermesAssertionException($"Expected method '{methodName}' to not be called, but it was.");
    }

    #endregion

    #region Events

    /// <summary>
    /// Assert that a specific event was raised.
    /// </summary>
    public static void EventWasRaised(WindowBackendRecording recording, string eventName)
    {
        if (!recording.EventWasRaised(eventName))
            throw new HermesAssertionException($"Expected event '{eventName}' to be raised, but it was not. Raised events: [{string.Join(", ", recording.Events.Select(e => e.EventName))}]");
    }

    /// <summary>
    /// Assert that a specific event was not raised.
    /// </summary>
    public static void EventWasNotRaised(WindowBackendRecording recording, string eventName)
    {
        if (recording.EventWasRaised(eventName))
            throw new HermesAssertionException($"Expected event '{eventName}' to not be raised, but it was.");
    }

    #endregion

    #region Custom Schemes

    /// <summary>
    /// Assert that a custom scheme was registered.
    /// </summary>
    public static void CustomSchemeRegistered(RecordingWindowBackend backend, string scheme)
    {
        if (!backend.HasCustomScheme(scheme))
            throw new HermesAssertionException($"Expected custom scheme '{scheme}' to be registered, but it was not.");
    }

    #endregion
}

/// <summary>
/// Exception thrown when a Hermes assertion fails.
/// </summary>
public class HermesAssertionException : Exception
{
    public HermesAssertionException(string message) : base(message)
    {
    }
}
