// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.WebView.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Hermes.Blazor.WebView;

/// <summary>
/// Registers the owned WebView host services, mirroring upstream's AddBlazorWebView with the
/// forked implementations.
/// </summary>
internal static class HermesWebViewServiceCollectionExtensions
{
    public static IServiceCollection AddHermesBlazorWebView(this IServiceCollection services)
    {
        services.AddLogging();
        services.TryAddScoped<IJSRuntime, WebViewJSRuntime>();
        services.TryAddScoped<INavigationInterception, WebViewNavigationInterception>();
        services.TryAddScoped<IScrollToLocationHash, WebViewScrollToLocationHash>();
        services.TryAddScoped<NavigationManager, WebViewNavigationManager>();
        services.TryAddScoped<IErrorBoundaryLogger, WebViewErrorBoundaryLogger>();
        services.TryAddScoped<ComponentStatePersistenceManager>();
        services.TryAddScoped<PersistentComponentState>(sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        services.AddSupplyValueFromQueryProvider();

        return services;
    }
}
