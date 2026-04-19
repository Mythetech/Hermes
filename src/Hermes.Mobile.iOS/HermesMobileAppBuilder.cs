// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Hermes.Contracts.Plugins;
using Hermes.Mobile.iOS.Plugins;
using Hermes.Mobile.iOS.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Mobile.iOS;

/// <summary>
/// Builder for configuring and constructing a <see cref="HermesMobileHost"/>.
/// Mirrors the shape of HermesBlazorAppBuilder so the mental model transfers between heads.
/// </summary>
public sealed class HermesMobileAppBuilder : IMobileBuilder<HermesMobileHost>
{
    private string _hostPage = "wwwroot/index.html";

    private HermesMobileAppBuilder()
    {
        Services = new ServiceCollection();
        Services.AddBlazorWebView();
        Services.AddSingleton<IClipboard, IOSClipboard>();
    }

    public static HermesMobileAppBuilder CreateDefault() => new();

    public IServiceCollection Services { get; }

    public RootComponentCollection RootComponents { get; } = new();

    IMobileBuilder<HermesMobileHost> IMobileBuilder<HermesMobileHost>.UseHostPage(string hostPage)
    {
        _hostPage = hostPage;
        return this;
    }

    public HermesMobileAppBuilder UseHostPage(string hostPage)
    {
        _hostPage = hostPage;
        return this;
    }

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    public HermesMobileHost Build()
    {
        var provider = Services.BuildServiceProvider();

        var contentRoot = Path.GetDirectoryName(_hostPage) ?? string.Empty;
        var hostPageRelative = Path.GetRelativePath(
            string.IsNullOrEmpty(contentRoot) ? "." : contentRoot,
            _hostPage);

        var fileProvider = new IOSAssetFileProvider(contentRoot);

        var components = RootComponents.GetComponents()
            .Select(c => (c.Type, c.Selector))
            .ToList();

        return new HermesMobileHost(provider, fileProvider, hostPageRelative, components);
    }
}
