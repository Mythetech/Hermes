// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Wraps each root component in smoke mode. The WebView renderer only finishes a batch once the page
/// acknowledges it, so this component's first after-render means the DOM really updated, and its
/// injected provider is the page's scope, which is where checks must resolve.
/// </summary>
internal sealed class SmokeRoot(HermesSmokeRuntime runtime, IServiceProvider scopedServices) : ComponentBase
{
    [Parameter]
    public Type ComponentType { get; set; } = default!;

    [Parameter]
    public IDictionary<string, object>? ComponentParameters { get; set; }

    internal static IDictionary<string, object?> CreateParameters(Type componentType, IDictionary<string, object?>? parameters) =>
        new Dictionary<string, object?>
        {
            [nameof(ComponentType)] = componentType,
            [nameof(ComponentParameters)] = parameters?.ToDictionary(p => p.Key, p => p.Value!),
        };

    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "The component types passed as ComponentType are kept by the app's own root registration (RootComponentCollection.Add<TComponent> and HermesRootComponents.Add(Type, ...) are annotated PublicConstructors | PublicProperties), not by an annotation on this property. DynamicComponent itself relies on the same assumption that its Type values come from assemblies that are not trimmed away.")]
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<DynamicComponent>(0);
        builder.AddComponentParameter(1, nameof(DynamicComponent.Type), ComponentType);
        builder.AddComponentParameter(2, nameof(DynamicComponent.Parameters), ComponentParameters);
        builder.CloseComponent();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        // Children render in the same batch but get their after-render callbacks after this one, so the
        // verdict has to wait for the batch to finish. Task.Yield would not: it posts from this UI
        // thread, and BeginInvoke called on the UI thread runs its callback inline on Windows, Linux and
        // the recording backend whatever the context's busy state. Only a post from a non-UI thread is
        // guaranteed to queue.
        await DeferPastCurrentRenderBatchAsync();
        await runtime.OnRootRenderedAsync(scopedServices);
    }

    /// <summary>
    /// Defers past the rest of the current render batch by posting the continuation to the calling
    /// SynchronizationContext from a thread-pool thread. A post from the UI thread can run inline:
    /// BeginInvoke called on the UI thread runs its callback directly on Windows, Linux and the
    /// recording backend, whatever the context's busy state. Only a post from a non-UI thread is
    /// guaranteed to queue.
    /// <para>
    /// <see cref="DeferredAwaitable"/> reports <c>IsCompleted</c> as <see langword="false"/> so the
    /// await always goes through <c>OnCompleted</c>: an awaitable that may already be complete when it
    /// is awaited, such as a finished <see cref="Task"/>, would resume inline on the UI thread without
    /// posting at all.
    /// </para>
    /// </summary>
    internal static DeferredAwaitable DeferPastCurrentRenderBatchAsync() => default;

    /// <summary>See <see cref="DeferPastCurrentRenderBatchAsync"/> for why this exists.</summary>
    internal readonly struct DeferredAwaitable
    {
        public DeferredAwaiter GetAwaiter() => default;

        internal readonly struct DeferredAwaiter : ICriticalNotifyCompletion
        {
            public bool IsCompleted => false;

            public void GetResult()
            {
            }

            public void OnCompleted(Action continuation) => Schedule(continuation);

            public void UnsafeOnCompleted(Action continuation) => Schedule(continuation);

            private static void Schedule(Action continuation)
            {
                var capturedContext = SynchronizationContext.Current;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    if (capturedContext is null)
                        continuation();
                    else
                        capturedContext.Post(_ => continuation(), null);
                });
            }
        }
    }
}
