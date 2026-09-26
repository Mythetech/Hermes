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

        // Children render in the same batch but get their after-render callbacks after this one. A
        // Task.Yield here would not reliably defer: its continuation is scheduled by calling
        // SynchronizationContext.Post synchronously, on this same UI thread call stack, and
        // HermesSynchronizationContext.Post runs inline whenever it is called from the UI thread and
        // the context is not already marked busy, which is exactly the state after the renderer's
        // after-render loop invokes this method directly (not through a nested Post/Send). That would
        // let this method's continuation, and the verdict, run before the other roots' after-render
        // callbacks in the same batch.
        await DeferPastCurrentRenderBatchAsync();
        await runtime.OnRootRenderedAsync(scopedServices);
    }

    /// <summary>
    /// Defers past the rest of the current render batch so that resuming here always re-enters the
    /// calling SynchronizationContext from off the UI thread, guaranteeing
    /// HermesSynchronizationContext.Post queues the continuation instead of running it inline.
    /// <para>
    /// Anything awaitable whose completion is a race (a completed-or-not <see cref="Task"/>, including
    /// <c>Task.Run(() => {})</c> and even <c>Task.Delay</c> under enough scheduler contention) is not
    /// safe here: the compiler-generated await only posts through the SynchronizationContext when the
    /// antecedent is still incomplete at the moment it is checked, and if the calling thread is ever
    /// delayed past that point (a busy thread pool, a loaded CI runner), the work can finish first and
    /// the whole continuation, including the eventual verdict, runs inline on the UI thread anyway.
    /// </para>
    /// <para>
    /// <see cref="DeferredAwaitable"/> instead reports <c>IsCompleted</c> as a hard-coded
    /// <see langword="false"/>, so the compiler can never take that shortcut: it always calls
    /// <c>OnCompleted</c>, and that registration schedules the resumption on the thread pool rather
    /// than invoking it (or posting through the SynchronizationContext) there and then. The eventual
    /// <c>SynchronizationContext.Post</c> call is therefore always made from that thread-pool callback,
    /// never from the UI thread, with no timing race involved.
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
