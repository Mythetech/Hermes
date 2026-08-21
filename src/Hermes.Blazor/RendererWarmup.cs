// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermes.Blazor;

/// <summary>
/// Renders a trivial framework-owned component through HtmlRenderer to force
/// JIT compilation of the renderer, render tree builder, and diff
/// infrastructure before the WebView attaches. Runs on a worker thread during
/// the WebKit process spawn wait; never executes user components.
/// </summary>
internal static class RendererWarmup
{
    public static void Run(IServiceProvider services)
    {
        try
        {
            var loggerFactory = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            var renderer = new HtmlRenderer(services, loggerFactory);
            try
            {
                renderer.Dispatcher.InvokeAsync(async () =>
                {
                    var output = await renderer.RenderComponentAsync<WarmupComponent>();
                    _ = output.ToHtmlString();
                }).GetAwaiter().GetResult();
            }
            finally
            {
                renderer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch
        {
            // Warmup is best-effort; a failure must never break startup.
        }
    }

    private sealed class WarmupComponent : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddContent(1, "warmup");
            builder.CloseElement();
        }
    }
}
