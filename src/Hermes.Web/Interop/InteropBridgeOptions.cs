// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;

namespace Hermes.Web.Interop;

public sealed class InteropBridgeOptions
{
    internal Dictionary<string, Func<object?[], Task<object?>>> InvokeHandlers { get; } = new();
    internal Dictionary<string, List<Action<object?>>> EventHandlers { get; } = new();

    public void Register(string method, Func<object?> handler)
    {
        InvokeHandlers[method] = _ => Task.FromResult(handler());
    }

    public void Register<TResult>(string method, Func<TResult> handler)
    {
        InvokeHandlers[method] = _ => Task.FromResult<object?>(handler());
    }

    [RequiresUnreferencedCode("Argument deserialization may require types that cannot be statically analyzed")]
    [RequiresDynamicCode("Argument deserialization may require runtime code generation")]
    public void Register<TArg, TResult>(string method, Func<TArg, TResult> handler)
    {
        InvokeHandlers[method] = args =>
        {
            var arg = InteropBridge.DeserializeArg<TArg>(args, 0);
            return Task.FromResult<object?>(handler(arg));
        };
    }

    public void RegisterAsync<TResult>(string method, Func<Task<TResult>> handler)
    {
        InvokeHandlers[method] = async _ =>
        {
            var result = await handler();
            return result;
        };
    }

    [RequiresUnreferencedCode("Argument deserialization may require types that cannot be statically analyzed")]
    [RequiresDynamicCode("Argument deserialization may require runtime code generation")]
    public void RegisterAsync<TArg, TResult>(string method, Func<TArg, Task<TResult>> handler)
    {
        InvokeHandlers[method] = async args =>
        {
            var arg = InteropBridge.DeserializeArg<TArg>(args, 0);
            var result = await handler(arg);
            return result;
        };
    }

    public void On(string eventName, Action handler)
    {
        GetOrCreateEventList(eventName).Add(_ => handler());
    }

    [RequiresUnreferencedCode("Event data deserialization may require types that cannot be statically analyzed")]
    [RequiresDynamicCode("Event data deserialization may require runtime code generation")]
    public void On<T>(string eventName, Action<T> handler)
    {
        GetOrCreateEventList(eventName).Add(data =>
        {
            var typed = InteropBridge.DeserializeData<T>(data);
            handler(typed);
        });
    }

    private List<Action<object?>> GetOrCreateEventList(string eventName)
    {
        if (!EventHandlers.TryGetValue(eventName, out var list))
        {
            list = new List<Action<object?>>();
            EventHandlers[eventName] = list;
        }
        return list;
    }
}
