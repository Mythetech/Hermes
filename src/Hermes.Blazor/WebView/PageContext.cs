// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Forked by Mythetech from dotnet/aspnetcore tag v10.0.11, src/Components/WebView/WebView/src/PageContext.cs.
// Modifications Copyright (c) Mythetech, licensed under the MIT License. See THIRD-PARTY-NOTICES.md.

#nullable disable warnings

using Hermes.Blazor.WebView.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Hermes.Blazor.WebView;

/// <summary>
/// Represents the services that are scoped to a single page load. Grouping them like this
/// means we don't have to check that each of them are available individually.
///
/// This has roughly the same role as a circuit in Blazor Server. One key difference is that,
/// for web views, the IPC channel is outside the page context, whereas in Blazor Server,
/// the IPC channel is within the circuit.
/// </summary>
internal sealed class PageContext : IAsyncDisposable
{
    private readonly AsyncServiceScope _serviceScope;

    public WebViewNavigationManager NavigationManager { get; }
    public WebViewJSRuntime JSRuntime { get; }
    public HermesWebRenderer Renderer { get; }
    public IServiceProvider ServiceProvider => _serviceScope.ServiceProvider;

    public PageContext(
        Dispatcher dispatcher,
        AsyncServiceScope serviceScope,
        IpcSender ipcSender,
        string baseUrl,
        string startUrl)
    {
        _serviceScope = serviceScope;

        NavigationManager = (WebViewNavigationManager)ServiceProvider.GetRequiredService<NavigationManager>();
        NavigationManager.AttachToWebView(ipcSender, baseUrl, startUrl);

        JSRuntime = (WebViewJSRuntime)ServiceProvider.GetRequiredService<IJSRuntime>();
        JSRuntime.AttachToWebView(ipcSender);

        var loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        Renderer = new HermesWebRenderer(ServiceProvider, dispatcher, ipcSender, loggerFactory, JSRuntime);

        var webViewScrollToLocationHash = (WebViewScrollToLocationHash)ServiceProvider.GetRequiredService<IScrollToLocationHash>();
        webViewScrollToLocationHash.AttachJSRuntime(JSRuntime);
    }

    public async ValueTask DisposeAsync()
    {
        await Renderer.DisposeAsync();
        await _serviceScope.DisposeAsync();
    }
}
