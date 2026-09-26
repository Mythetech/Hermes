// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Wraps each root component in smoke mode. The WebView renderer only finishes a batch once the page
/// acknowledges it, so this component's first after-render means the DOM really updated, and its
/// injected provider is the page's scope, which is where checks must resolve.
/// </summary>
internal sealed class SmokeRoot : ComponentBase
{
    [Parameter]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public Type ComponentType { get; set; } = default!;

    [Parameter]
    public IDictionary<string, object>? ComponentParameters { get; set; }

    [Inject]
    private HermesSmokeRuntime Runtime { get; set; } = default!;

    [Inject]
    private IServiceProvider ScopedServices { get; set; } = default!;

    internal static IDictionary<string, object?> CreateParameters(Type componentType, IDictionary<string, object?>? parameters) =>
        new Dictionary<string, object?>
        {
            [nameof(ComponentType)] = componentType,
            [nameof(ComponentParameters)] = parameters?.ToDictionary(p => p.Key, p => p.Value!),
        };

    [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "ComponentType carries its own DynamicallyAccessedMembers contract (public constructors and properties), which is what smoke-mode root components need preserved; DynamicComponent.Type itself is documented as expecting types from assemblies that are not trimmed away.")]
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

        // Children render in the same batch but get their after-render callbacks after this one; yielding
        // lets them run first, so anything they do on first render lands before the verdict.
        await Task.Yield();
        await Runtime.OnRootRenderedAsync(ScopedServices);
    }
}
