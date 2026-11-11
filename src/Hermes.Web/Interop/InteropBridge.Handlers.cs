// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Web.Interop;

public sealed partial class InteropBridge
{
    /// <summary>
    /// Handles calls to <paramref name="method"/> from the page. The handler runs on the thread pool, its
    /// arguments are read from the call's JSON arguments in order, and its result is the call's result.
    /// </summary>
    /// <exception cref="ArgumentException">A handler for <paramref name="method"/> is already registered.</exception>
    public void Handle<TResult>(string method, Func<TResult> handler) =>
        AddMethod(method, handler, [], typeof(TResult), (_, _) => Task.FromResult<object?>(handler()));

    /// <inheritdoc cref="Handle{TResult}(string, Func{TResult})"/>
    public void Handle<T1, TResult>(string method, Func<T1, TResult> handler) =>
        AddMethod(method, handler, [typeof(T1)], typeof(TResult), (a, _) => Task.FromResult<object?>(handler((T1)a[0]!)));

    /// <inheritdoc cref="Handle{TResult}(string, Func{TResult})"/>
    public void Handle<T1, T2, TResult>(string method, Func<T1, T2, TResult> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2)], typeof(TResult),
            (a, _) => Task.FromResult<object?>(handler((T1)a[0]!, (T2)a[1]!)));

    /// <inheritdoc cref="Handle{TResult}(string, Func{TResult})"/>
    public void Handle<T1, T2, T3, TResult>(string method, Func<T1, T2, T3, TResult> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3)], typeof(TResult),
            (a, _) => Task.FromResult<object?>(handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!)));

    /// <inheritdoc cref="Handle{TResult}(string, Func{TResult})"/>
    public void Handle<T1, T2, T3, T4, TResult>(string method, Func<T1, T2, T3, T4, TResult> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3), typeof(T4)], typeof(TResult),
            (a, _) => Task.FromResult<object?>(handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, (T4)a[3]!)));

    /// <summary>
    /// Handles calls to <paramref name="method"/> from the page with an async handler. A handler that takes a
    /// <see cref="CancellationToken"/> sees it cancelled when the window closes.
    /// </summary>
    /// <exception cref="ArgumentException">A handler for <paramref name="method"/> is already registered.</exception>
    public void HandleAsync<TResult>(string method, Func<Task<TResult>> handler) =>
        AddMethod(method, handler, [], typeof(TResult), async (_, _) => (object?)await handler());

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<TResult>(string method, Func<CancellationToken, Task<TResult>> handler) =>
        AddMethod(method, handler, [], typeof(TResult), async (_, ct) => (object?)await handler(ct));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, TResult>(string method, Func<T1, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1)], typeof(TResult), async (a, _) => (object?)await handler((T1)a[0]!));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, TResult>(string method, Func<T1, CancellationToken, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1)], typeof(TResult), async (a, ct) => (object?)await handler((T1)a[0]!, ct));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, T2, TResult>(string method, Func<T1, T2, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2)], typeof(TResult),
            async (a, _) => (object?)await handler((T1)a[0]!, (T2)a[1]!));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, T2, TResult>(string method, Func<T1, T2, CancellationToken, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2)], typeof(TResult),
            async (a, ct) => (object?)await handler((T1)a[0]!, (T2)a[1]!, ct));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, T2, T3, TResult>(string method, Func<T1, T2, T3, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3)], typeof(TResult),
            async (a, _) => (object?)await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, T2, T3, TResult>(string method, Func<T1, T2, T3, CancellationToken, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3)], typeof(TResult),
            async (a, ct) => (object?)await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, ct));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, T2, T3, T4, TResult>(string method, Func<T1, T2, T3, T4, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3), typeof(T4)], typeof(TResult),
            async (a, _) => (object?)await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, (T4)a[3]!));

    /// <inheritdoc cref="HandleAsync{TResult}(string, Func{Task{TResult}})"/>
    public void HandleAsync<T1, T2, T3, T4, TResult>(string method, Func<T1, T2, T3, T4, CancellationToken, Task<TResult>> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3), typeof(T4)], typeof(TResult),
            async (a, ct) => (object?)await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, (T4)a[3]!, ct));

    /// <summary>
    /// Handles calls to <paramref name="method"/> from the page with an async handler that has no result; the
    /// call resolves with null. A handler that takes a <see cref="CancellationToken"/> sees it cancelled when the window closes.
    /// </summary>
    /// <exception cref="ArgumentException">A handler for <paramref name="method"/> is already registered.</exception>
    public void HandleAsync(string method, Func<Task> handler) =>
        AddMethod(method, handler, [], null, async (_, _) =>
        {
            await handler();
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync(string method, Func<CancellationToken, Task> handler) =>
        AddMethod(method, handler, [], null, async (_, ct) =>
        {
            await handler(ct);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1>(string method, Func<T1, Task> handler) =>
        AddMethod(method, handler, [typeof(T1)], null, async (a, _) =>
        {
            await handler((T1)a[0]!);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1>(string method, Func<T1, CancellationToken, Task> handler) =>
        AddMethod(method, handler, [typeof(T1)], null, async (a, ct) =>
        {
            await handler((T1)a[0]!, ct);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1, T2>(string method, Func<T1, T2, Task> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2)], null, async (a, _) =>
        {
            await handler((T1)a[0]!, (T2)a[1]!);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1, T2>(string method, Func<T1, T2, CancellationToken, Task> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2)], null, async (a, ct) =>
        {
            await handler((T1)a[0]!, (T2)a[1]!, ct);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1, T2, T3>(string method, Func<T1, T2, T3, Task> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3)], null, async (a, _) =>
        {
            await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1, T2, T3>(string method, Func<T1, T2, T3, CancellationToken, Task> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3)], null, async (a, ct) =>
        {
            await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, ct);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1, T2, T3, T4>(string method, Func<T1, T2, T3, T4, Task> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3), typeof(T4)], null, async (a, _) =>
        {
            await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, (T4)a[3]!);
            return null;
        });

    /// <inheritdoc cref="HandleAsync(string, Func{Task})"/>
    public void HandleAsync<T1, T2, T3, T4>(string method, Func<T1, T2, T3, T4, CancellationToken, Task> handler) =>
        AddMethod(method, handler, [typeof(T1), typeof(T2), typeof(T3), typeof(T4)], null, async (a, ct) =>
        {
            await handler((T1)a[0]!, (T2)a[1]!, (T3)a[2]!, (T4)a[3]!, ct);
            return null;
        });

    /// <summary>
    /// Handles <paramref name="eventName"/> events from the page. Event handlers run one at a time, in the order
    /// the events arrived. Dispose the result to stop handling them.
    /// </summary>
    public IDisposable On(string eventName, Action handler) =>
        AddEvent(eventName, handler, null, _ =>
        {
            handler();
            return Task.CompletedTask;
        });

    /// <inheritdoc cref="On(string, Action)"/>
    public IDisposable On<T>(string eventName, Action<T> handler) =>
        AddEvent(eventName, handler, typeof(T), data =>
        {
            handler((T)data!);
            return Task.CompletedTask;
        });

    /// <inheritdoc cref="On(string, Action)"/>
    public IDisposable On(string eventName, Func<Task> handler) =>
        AddEvent(eventName, handler, null, _ => handler());

    /// <inheritdoc cref="On(string, Action)"/>
    public IDisposable On<T>(string eventName, Func<T, Task> handler) =>
        AddEvent(eventName, handler, typeof(T), data => handler((T)data!));

    /// <summary>Sends <paramref name="eventName"/> to the page with no data.</summary>
    public void Send(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        Post(BridgeJson.Event(eventName, null, null));
    }

    /// <summary>Sends <paramref name="eventName"/> to the page with <paramref name="data"/> as its payload.</summary>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> has no JSON metadata in <see cref="SerializerOptions"/>.</exception>
    public void Send<T>(string eventName, T data)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        EnsureSerializerReady();
        Post(BridgeJson.Event(eventName, data, BridgeJson.GetTypeInfo(SerializerOptions, typeof(T))));
    }

    private void EnsureSerializerReady()
    {
        if (SerializerOptions.IsReadOnly)
            return;

        lock (_gate)
            BridgeJson.Freeze(SerializerOptions, _useReflection);
    }

    private void AddMethod(
        string method,
        Delegate handler,
        Type[] parameterTypes,
        Type? resultType,
        Func<object?[], CancellationToken, Task<object?>> invoke)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            if (!_methods.TryAdd(method, new MethodHandler(parameterTypes, resultType, invoke)))
                throw new ArgumentException($"A handler for '{method}' is already registered.", nameof(method));
        }
    }

    private IDisposable AddEvent(string eventName, Delegate handler, Type? dataType, Func<object?, Task> invoke)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new EventSubscription(dataType, invoke);
        lock (_gate)
        {
            if (!_events.TryGetValue(eventName, out var list))
                _events[eventName] = list = [];
            list.Add(subscription);
        }

        return new Subscription(this, eventName, subscription);
    }

    private void RemoveEvent(string eventName, EventSubscription subscription)
    {
        lock (_gate)
        {
            if (!_events.TryGetValue(eventName, out var list))
                return;

            list.Remove(subscription);
            if (list.Count == 0)
                _events.Remove(eventName);
        }
    }

    private sealed class MethodHandler(Type[] parameterTypes, Type? resultType, Func<object?[], CancellationToken, Task<object?>> invoke)
    {
        public Type[] ParameterTypes { get; } = parameterTypes;

        public Type? ResultType { get; } = resultType;

        public Func<object?[], CancellationToken, Task<object?>> Invoke { get; } = invoke;
    }

    private sealed class EventSubscription(Type? dataType, Func<object?, Task> invoke)
    {
        public Type? DataType { get; } = dataType;

        public Func<object?, Task> Invoke { get; } = invoke;
    }

    private sealed class Subscription(InteropBridge bridge, string eventName, EventSubscription subscription) : IDisposable
    {
        public void Dispose() => bridge.RemoveEvent(eventName, subscription);
    }
}
