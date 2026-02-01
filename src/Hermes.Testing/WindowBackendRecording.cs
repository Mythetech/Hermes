namespace Hermes.Testing;

/// <summary>
/// Represents a single recorded method call on the window backend.
/// </summary>
public sealed record MethodCall
{
    /// <summary>
    /// The name of the method that was called.
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// The arguments passed to the method.
    /// </summary>
    public object?[] Arguments { get; init; } = [];

    /// <summary>
    /// When the method was called.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a recorded property change on the window backend.
/// </summary>
public sealed record PropertyChange
{
    /// <summary>
    /// The name of the property that was changed.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// The previous value of the property.
    /// </summary>
    public object? OldValue { get; init; }

    /// <summary>
    /// The new value of the property.
    /// </summary>
    public object? NewValue { get; init; }

    /// <summary>
    /// When the property was changed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a recorded event that was raised.
/// </summary>
public sealed record RecordedEvent
{
    /// <summary>
    /// The name of the event.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// The arguments passed with the event.
    /// </summary>
    public object?[] Arguments { get; init; } = [];

    /// <summary>
    /// When the event was raised.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Contains all recorded interactions with a window backend.
/// </summary>
public sealed class WindowBackendRecording
{
    private readonly List<MethodCall> _methodCalls = [];
    private readonly List<PropertyChange> _propertyChanges = [];
    private readonly List<RecordedEvent> _events = [];
    private readonly List<string> _webMessagesReceived = [];
    private readonly List<string> _webMessagesSent = [];
    private readonly List<string> _navigations = [];
    private string? _lastDragAction;

    /// <summary>
    /// All method calls in chronological order.
    /// </summary>
    public IReadOnlyList<MethodCall> MethodCalls => _methodCalls;

    /// <summary>
    /// All property changes in chronological order.
    /// </summary>
    public IReadOnlyList<PropertyChange> PropertyChanges => _propertyChanges;

    /// <summary>
    /// All events raised in chronological order.
    /// </summary>
    public IReadOnlyList<RecordedEvent> Events => _events;

    /// <summary>
    /// All web messages received from JavaScript.
    /// </summary>
    public IReadOnlyList<string> WebMessagesReceived => _webMessagesReceived;

    /// <summary>
    /// All web messages sent to JavaScript.
    /// </summary>
    public IReadOnlyList<string> WebMessagesSent => _webMessagesSent;

    /// <summary>
    /// All URLs navigated to.
    /// </summary>
    public IReadOnlyList<string> Navigations => _navigations;

    /// <summary>
    /// The last drag action detected (drag, double-click, no-drag).
    /// Used for testing custom titlebar drag detection.
    /// </summary>
    public string? LastDragAction => _lastDragAction;

    internal void RecordMethodCall(string methodName, params object?[] arguments)
    {
        _methodCalls.Add(new MethodCall
        {
            MethodName = methodName,
            Arguments = arguments
        });
    }

    internal void RecordPropertyChange(string propertyName, object? oldValue, object? newValue)
    {
        _propertyChanges.Add(new PropertyChange
        {
            PropertyName = propertyName,
            OldValue = oldValue,
            NewValue = newValue
        });
    }

    internal void RecordEvent(string eventName, params object?[] arguments)
    {
        _events.Add(new RecordedEvent
        {
            EventName = eventName,
            Arguments = arguments
        });
    }

    internal void RecordWebMessageReceived(string message)
    {
        _webMessagesReceived.Add(message);
    }

    internal void RecordWebMessageSent(string message)
    {
        _webMessagesSent.Add(message);
    }

    internal void RecordNavigation(string url)
    {
        _navigations.Add(url);
    }

    internal void RecordDragAction(string action)
    {
        _lastDragAction = action;
    }

    /// <summary>
    /// Clear all recorded data.
    /// </summary>
    public void Clear()
    {
        _methodCalls.Clear();
        _propertyChanges.Clear();
        _events.Clear();
        _webMessagesReceived.Clear();
        _webMessagesSent.Clear();
        _navigations.Clear();
        _lastDragAction = null;
    }

    /// <summary>
    /// Check if a method was called with the given name.
    /// </summary>
    public bool MethodWasCalled(string methodName) =>
        _methodCalls.Any(c => c.MethodName == methodName);

    /// <summary>
    /// Check if a URL was navigated to.
    /// </summary>
    public bool NavigatedTo(string url) =>
        _navigations.Any(n => n.Equals(url, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Check if a URL matching a pattern was navigated to.
    /// </summary>
    public bool NavigatedToPattern(string pattern) =>
        _navigations.Any(n => n.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Check if a web message was sent matching a pattern.
    /// </summary>
    public bool SentWebMessageMatching(string pattern) =>
        _webMessagesSent.Any(m => m.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Check if a web message was received matching a pattern.
    /// </summary>
    public bool ReceivedWebMessageMatching(string pattern) =>
        _webMessagesReceived.Any(m => m.Contains(pattern, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Check if an event was raised with the given name.
    /// </summary>
    public bool EventWasRaised(string eventName) =>
        _events.Any(e => e.EventName == eventName);
}
