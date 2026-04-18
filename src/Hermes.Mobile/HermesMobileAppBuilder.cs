// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Hermes.Contracts.Plugins;
using Hermes.Mobile.Plugins;
using Hermes.Mobile.WebView;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Mobile;

/// <summary>
/// Builder for configuring and constructing a <see cref="HermesMobileHost"/>.
/// Mirrors the shape of HermesBlazorAppBuilder so the mental model transfers between heads.
/// </summary>
public sealed class HermesMobileAppBuilder
{
    private string _hostPage = "wwwroot/index.html";
    private const string AppScheme = "app";
    private const string AppHost = "localhost";

    private HermesMobileAppBuilder()
    {
        Services = new ServiceCollection();
        Services.AddSingleton<IClipboard, IOSClipboard>();
    }

    public static HermesMobileAppBuilder CreateDefault() => new();

    public IServiceCollection Services { get; }

    public RootComponentCollection RootComponents { get; } = new();

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
        var appBaseUri = new Uri($"{AppScheme}://{AppHost}/");

        var components = RootComponents.GetComponents()
            .Select(c => (c.Type, c.Selector))
            .ToList();

        return new HermesMobileHost(provider, fileProvider, appBaseUri, hostPageRelative, components);
    }
}

public sealed class RootComponentCollection
{
    private readonly List<(Type Type, string Selector)> _components = new();

    public void Add<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] TComponent>(string selector)
        where TComponent : IComponent
    {
        _components.Add((typeof(TComponent), selector));
    }

    internal IEnumerable<(Type Type, string Selector)> GetComponents() => _components;
}
